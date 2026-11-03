using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;

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

    private List<Candidate> Objects { get; } = [];

    private Dictionary<(ulong Frame, ulong Address), int> Index { get; } = [];

    private Dictionary<ulong, List<int>> Owned { get; } = [];

    private Dictionary<ulong, List<int>> Pending { get; } = [];

    private List<(byte[] Data, List<StackSnapshotRun> Runs)> Chunks { get; } = [];

    private byte[] Data { get; set; } = new byte[ChunkSize];

    private List<StackSnapshotRun> Runs { get; set; } = [];

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
            Handoff(call.Arguments[InstanceArgument], call, replay, isReturned: false);
        }

        foreach (var declared in function.Declared)
        {
            if (declared.ArgumentSlot < ArgumentLayout.RegisterCount)
            {
                Handoff(call.Arguments[declared.ArgumentSlot], call, replay, isReturned: false);
            }
        }

        foreach (var slot in function.Pointers)
        {
            Handoff(TraceHookArgumentReader.GetArgument(replay, call, slot), call, replay, isReturned: false);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Pending.Remove(call.Id, out var pending))
        {
            var position = GetPositionBefore(call.Position);

            foreach (var index in pending)
            {
                Snapshot(index, position, replay);
            }
        }

        if (!Operations.TryGetValue(call.FunctionAddress, out var function) || !IsWatched(function))
        {
            return;
        }

        foreach (var declared in function.Declared)
        {
            if (declared.ArgumentSlot == ArgumentLayout.ReturnRegister)
            {
                Handoff(call.ReturnValue, call, replay, isReturned: true);
            }
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Pending.Remove(call.Id);

    public void OnFrameEnded(in TraceHookCall call)
    {
        if (!Owned.Remove(call.Id, out var owned))
        {
            return;
        }

        foreach (var index in owned)
        {
            var target = Objects[index];

            target.EndPosition = call.Position;

            target.Image = null;

            target.Known = null;

            Index.Remove((call.Id, target.Address));
        }
    }

    public StackSnapshotResult Build()
    {
        CloseChunk();

        var kept = new int[Objects.Count];

        var objects = new List<StackObject>();

        for (var index = 0; index < Objects.Count; index++)
        {
            var target = Objects[index];

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
            var clipped = new List<StackSnapshotRun>(runs.Count);

            foreach (var run in runs)
            {
                var length = Math.Min(run.Length, Objects[run.Object].Size - run.Offset);

                if (length > 0)
                {
                    clipped.Add(run with { Object = kept[run.Object], Length = (ushort)length });
                }
            }

            chunks.Add(new DataChunk<StackSnapshotRun>([.. clipped], data));
        }

        return new StackSnapshotResult([.. objects], [.. chunks], SnapshotCount);
    }

    private static bool IsWatched(AddressMapFunction function)
        => function.Operation is AddressMapOperation.Constructed or AddressMapOperation.Visited
           || function.Declared.Length > 0
           || function.Pointers.Length > 0;

    private static TracePosition GetPositionBefore(TracePosition position)
        => position.Steps > 0 ? new TracePosition(position.Sequence, position.Steps - 1) : position;

    private void Handoff(ulong address, in TraceHookCall call, ITraceReplay replay, bool isReturned)
    {
        if (address < TraceAddress.MinAddress || address < call.StackPointer)
        {
            return;
        }

        var frames = replay.GetFrames(call.Thread);

        var owner = TraceHookFrameLookup.GetOwnerIndex(frames, address);

        if (owner < 0)
        {
            return;
        }

        var position = GetPositionBefore(call.Position);

        var index = GetObject(frames[owner], address, position, call.Thread, replay);

        Snapshot(index, position, replay);

        if (isReturned)
        {
            return;
        }

        if (!Pending.TryGetValue(call.Id, out var pending))
        {
            pending = [];

            Pending[call.Id] = pending;
        }

        if (!pending.Contains(index))
        {
            pending.Add(index);
        }
    }

    private int GetObject(TraceHookFrame frame, ulong address, TracePosition position, uint thread, ITraceReplay replay)
    {
        if (Index.TryGetValue((frame.Id, address), out var index))
        {
            return index;
        }

        index = Objects.Count;

        Index[(frame.Id, address)] = index;

        if (!Owned.TryGetValue(frame.Id, out var owned))
        {
            owned = [];

            Owned[frame.Id] = owned;

            replay.WatchFrame(thread, frame.Id);
        }

        var size = (int)Math.Min(frame.StackPointer - address, MaximumSize);

        foreach (var other in owned)
        {
            var candidate = Objects[other];

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

        Objects.Add(new Candidate(address, size, thread, frame.CallNodeIndex, position));

        return index;
    }

    private void Snapshot(int index, TracePosition position, ITraceReplay replay)
    {
        var target = Objects[index];

        if (target.Size <= 0)
        {
            return;
        }

        SnapshotCount++;

        target.Image ??= new byte[target.Size];

        target.Known ??= new bool[target.Size];

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

        Chunks.Add((Data[..DataLength], Runs));

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

        public byte[]? Image { get; set; }

        public bool[]? Known { get; set; }

        public bool IsUnchanged(int offset, byte value) => Known![offset] && Image![offset] == value;
    }
}
