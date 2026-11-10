using System.Buffers;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;

/// <summary>
/// Capture of stack objects
/// </summary>
/// <remarks>
/// StackSnapshotHook captures the contents of stack objects — local/temporary objects in a caller's frame whose address is passed down
/// into a watched (lower) frame.
///
/// At a high level, it identifies addresses to track, derives the size of the objects, and takes snapshots of the memory contents at the
/// point of the call and return, following the stack lifecycle.
///
/// As the replay moves forward through the trace, frames are pushed and popped from the stack. The address space runs from high to low, so
/// lower frames get lower addresses.
///
/// Functions are watched if:
///
/// 1. The Operation is marked as Constructed or Visited (classified by AddressMapFunctionResolver)
///    -> OnEntry: RCX (this) address is tracked
///
/// 2. It has declared arguments
///    -> OnEntry: each declared register argument address is tracked
///    -> OnReturn: the return value is tracked if the return register is a declared slot
///
/// 3. It has pointer arguments
///    -> OnEntry: each pointer argument is read and the result is tracked
///
/// For the tracking/snapshot process:
///
/// 1. Each address is first validated to see if it is at or above the current stack pointer. This is a quick check to eliminate addresses
///    that are not on the stack (e.g. a heap object) or beyond the current execution point.
///
/// 2. The owner frame, where the address is located, is found by searching top-down in the current thread frame stack. If a frame is not
///    found the tracking will exit.
///
/// 3. Get or create a Candidate tracking object for the address. The Candidate is a temporary object that holds the address, size, thread,
///    owner frame and first position. Candidate is keyed on owner frame + address.
///
///    - If an existing Candidate is found it is returned
///
///    - If not found a new Candidate is created with a size defined by the distance between the address and the owner frame stack pointer
///      (frame.StackPointer - address), capped at a maximum size of 1 KB.
///
///      - Candidates cannot overlap as they represent a discrete object. The new candidate is referenced against existing candidates and
///        either can be shrunk to stop at the point the next candidate starts.
///
///      - The new candidate is added to the list of candidates and the frame's owned candidates.
///
/// 4. The first candidate in a frame sets a watch on the owner frame. Other candidates at different addresses on the same frame share this
///    watch. The watch triggers the OnFrameEnded event when the frame is popped. Candidates for the frame are marked as ended and removed
///    from the index.
///
/// 5. The position before the current position is captured and a snapshot is taken at this point (pre-call state from OnEntry, post-call
///    state from OnReturn).
///
///      - Snapshot compares the current bytes against the last known image and saves the delta bytes and linked tracking StackSnapshotRun
///        in the Chunks list - later converted to an immutable DataChunk for the final StackSnapshotResult.
///
/// 6. If tracking started in OnEntry then the Candidate index is added to a list of pending snapshots for the call. OnReturn checks the
///    pending list and snapshots the Candidate again at the return position, capturing post-call state. OnAbandoned discards the pending
///    list if the call never completes.
///
/// After the trace is complete, Build() is called to produce the final StackSnapshotResult. Candidates with a non-zero size are converted
/// to StackObjects, and the Chunks are converted to DataChunks of StackSnapshotRun, with each run clipped to the final object size and
/// remapped to the surviving StackObject indexes.
/// </remarks>
internal sealed class StackSnapshotHook(Dictionary<ulong, AddressMapFunction> operations) : ITraceHook
{
    private const int InstanceArgument = 0;

    private const int MaximumSize = 0x400;

    private const int ChunkSize = 1 << 20;

    private const int Alignment = sizeof(ulong);

    public IEnumerable<TraceHookFunction> Functions
        => Operations.Where(o => IsWatched(o.Value))
                     .Select(o => new TraceHookFunction(o.Key, TraceHookEvents.Entry | TraceHookEvents.Return));

    private Dictionary<ulong, AddressMapFunction> Operations { get; } = operations;

    private List<Candidate> Candidates { get; } = [];

    private Dictionary<(ulong Frame, ulong Address), int> Index { get; } = [];

    private Dictionary<ulong, List<int>> FrameCandidates { get; } = [];

    private Dictionary<ulong, List<int>> PendingSnapshots { get; } = [];

    private List<(byte[] Data, ChunkedList<StackSnapshotRun> Runs)> Chunks { get; } = [];

    private byte[] Data { get; set; } = new byte[ChunkSize];

    private ChunkedList<StackSnapshotRun> Runs { get; set; } = [];

    private ListPool<int> Lists { get; } = new();

    private int DataLength { get; set; }

    private long SnapshotCount { get; set; }

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Operations.TryGetValue(call.FunctionAddress, out var function) || !IsWatched(function))
        {
            return;
        }

        if (function.Operation is AddressMapOperation.Constructed or AddressMapOperation.Visited)
        {
            Track(call.Arguments[InstanceArgument], call, replay, isReturned: false);
        }

        foreach (var declared in function.DeclaredClasses)
        {
            if (declared.ArgumentSlot < ArgumentLayout.RegisterCount)
            {
                Track(call.Arguments[declared.ArgumentSlot], call, replay, isReturned: false);
            }
        }

        foreach (var slot in function.Pointers)
        {
            Track(TraceHookArgumentReader.GetArgument(replay, call, slot), call, replay, isReturned: false);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (PendingSnapshots.Remove(call.CallId, out var pending))
        {
            var position = GetPositionBefore(call.Position);

            foreach (var index in pending)
            {
                Snapshot(index, position, replay);
            }

            Lists.Return(pending);
        }

        if (!Operations.TryGetValue(call.FunctionAddress, out var function) || !IsWatched(function))
        {
            return;
        }

        foreach (var declared in function.DeclaredClasses)
        {
            if (declared.ArgumentSlot == ArgumentLayout.ReturnArgumentSlot)
            {
                Track(call.ReturnValue, call, replay, isReturned: true);
            }
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        if (PendingSnapshots.Remove(call.CallId, out var pending))
        {
            Lists.Return(pending);
        }
    }

    public void OnFrameEnded(in TraceHookCall call)
    {
        if (!FrameCandidates.Remove(call.CallId, out var owned))
        {
            return;
        }

        foreach (var index in owned)
        {
            var target = Candidates[index];

            target.EndPosition = call.Position;

            target.ReleaseImage();

            Index.Remove((call.CallId, target.Address));
        }

        Lists.Return(owned);
    }

    public StackSnapshotResult Build()
    {
        CloseChunk();

        var kept = new int[Candidates.Count];

        var objects = new List<StackObject>();

        for (var index = 0; index < Candidates.Count; index++)
        {
            var target = Candidates[index];

            kept[index] = target.Size > 0 ? objects.Count : -1;

            if (target.Size > 0)
            {
                objects.Add(new StackObject(target.Address,
                                            target.Size,
                                            target.Thread,
                                            target.OwnerCallNodeIndex,
                                            target.FirstPosition,
                                            target.EndPosition));
            }
        }

        var chunks = new List<DataChunk<StackSnapshotRun>>(Chunks.Count);

        foreach (var (data, runs) in Chunks)
        {
            var count = 0;

            for (var index = 0; index < runs.Count; index++)
            {
                if (GetClippedLength(runs[index]) > 0)
                {
                    count++;
                }
            }

            var clipped = new StackSnapshotRun[count];

            var next = 0;

            for (var index = 0; index < runs.Count; index++)
            {
                var run = runs[index];

                if (GetClippedLength(run) is > 0 and var length)
                {
                    clipped[next++] = run with { Object = kept[run.Object], Length = (ushort)length };
                }
            }

            chunks.Add(new DataChunk<StackSnapshotRun>(clipped, data));
        }

        return new StackSnapshotResult([.. objects], [.. chunks], SnapshotCount);
    }

    private static bool IsWatched(AddressMapFunction function)
        => function.Operation is AddressMapOperation.Constructed or AddressMapOperation.Visited
           || function.DeclaredClasses.Length > 0
           || function.Pointers.Length > 0;

    private static TracePosition GetPositionBefore(TracePosition position)
        => position.Steps > 0 ? new TracePosition(position.Sequence, position.Steps - 1) : position;

    private int GetClippedLength(StackSnapshotRun run) => Math.Min(run.Length, Candidates[run.Object].Size - run.Offset);

    private void Track(ulong address, in TraceHookCall call, ITraceReplay replay, bool isReturned)
    {
        if (address < TraceAddress.MinimumAddress || address < call.StackPointer)
        {
            return;
        }

        var frames = replay.GetFrames(call.Thread);

        var owner = TraceHookFrame.GetOwnerIndex(frames, address);

        if (owner < 0)
        {
            return;
        }

        var position = GetPositionBefore(call.Position);

        var index = GetCandidateIndex(frames[owner], address, position, call.Thread, replay);

        Snapshot(index, position, replay);

        if (isReturned)
        {
            return;
        }

        if (!PendingSnapshots.TryGetValue(call.CallId, out var pending))
        {
            pending = Lists.Rent();

            PendingSnapshots[call.CallId] = pending;
        }

        if (!pending.Contains(index))
        {
            pending.Add(index);
        }
    }

    private int GetCandidateIndex(TraceHookFrame frame, ulong address, TracePosition position, uint thread, ITraceReplay replay)
    {
        if (Index.TryGetValue((frame.CallId, address), out var index))
        {
            return index;
        }

        index = Candidates.Count;

        Index[(frame.CallId, address)] = index;

        if (!FrameCandidates.TryGetValue(frame.CallId, out var owned))
        {
            owned = Lists.Rent();

            FrameCandidates[frame.CallId] = owned;

            replay.WatchFrame(thread, frame.CallId);
        }

        var size = (int)Math.Min(frame.StackPointer - address, MaximumSize);

        foreach (var other in owned)
        {
            var candidate = Candidates[other];

            if (candidate.Address > address)
            {
                size = (int)Math.Min((ulong)size, candidate.Address - address);
            }
            else if (candidate.Address + (ulong)candidate.Size > address)
            {
                candidate.Size = (int)(address - candidate.Address);
            }
        }

        owned.Add(index);

        Candidates.Add(new Candidate(address, size, thread, frame.CallNodeIndex, position));

        return index;
    }

    private void Snapshot(int index, TracePosition position, ITraceReplay replay)
    {
        var target = Candidates[index];

        if (target.Size <= 0)
        {
            return;
        }

        SnapshotCount++;

        target.RentImage();

        Span<byte> buffer = stackalloc byte[MaximumSize];

        var bytes = buffer[..target.Size];

        var offset = 0;

        while (offset < bytes.Length)
        {
            var read = replay.Read(target.Address + (ulong)offset, bytes[offset..]);

            AddChanges(index, target, position, offset, bytes.Slice(offset, read));

            offset += read;

            if (offset < bytes.Length)
            {
                offset += Math.Min(Alignment - offset % Alignment, bytes.Length - offset);
            }
        }
    }

    private void AddChanges(int index, Candidate target, TracePosition position, int start, ReadOnlySpan<byte> bytes)
    {
        var cursor = 0;

        while (cursor < bytes.Length)
        {
            if (target.IsUnchanged(start + cursor, bytes[cursor]))
            {
                cursor++;

                continue;
            }

            var from = cursor;

            while (cursor < bytes.Length && !target.IsUnchanged(start + cursor, bytes[cursor]))
            {
                cursor++;
            }

            var run = bytes[from..cursor];

            run.CopyTo(target.Image!.AsSpan(start + from));

            target.Known!.AsSpan(start + from, run.Length).Fill(true);

            AddRun(index, position, start + from, run);
        }
    }

    private void AddRun(int index, TracePosition position, int offset, ReadOnlySpan<byte> run)
    {
        if (DataLength + run.Length > Data.Length)
        {
            CloseChunk();
        }

        run.CopyTo(Data.AsSpan(DataLength));

        Runs.Add(new StackSnapshotRun(index, position, offset, DataLength, (ushort)run.Length));

        DataLength += run.Length;
    }

    private void CloseChunk()
    {
        if (Runs.Count == 0)
        {
            return;
        }

        Chunks.Add((DataLength * 2 >= Data.Length ? Data : Data[..DataLength], Runs));

        Data = new byte[ChunkSize];

        Runs = [];

        DataLength = 0;
    }

    private sealed class Candidate(ulong address, int size, uint thread, int ownerCallNodeIndex, TracePosition firstPosition)
    {
        public ulong Address { get; } = address;

        public int Size { get; set; } = size;

        public uint Thread { get; } = thread;

        public int OwnerCallNodeIndex { get; } = ownerCallNodeIndex;

        public TracePosition FirstPosition { get; } = firstPosition;

        public TracePosition EndPosition { get; set; } = TracePosition.Unknown;

        public byte[]? Image { get; private set; }

        public bool[]? Known { get; private set; }

        public bool IsUnchanged(int offset, byte value) => Known![offset] && Image![offset] == value;

        public void RentImage()
        {
            if (Image is not null)
            {
                return;
            }

            Image = ArrayPool<byte>.Shared.Rent(Size);

            Known = ArrayPool<bool>.Shared.Rent(Size);

            Known.AsSpan(0, Size).Clear();
        }

        public void ReleaseImage()
        {
            if (Image is null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(Image);

            ArrayPool<bool>.Shared.Return(Known!);

            Image = null;

            Known = null;
        }
    }
}
