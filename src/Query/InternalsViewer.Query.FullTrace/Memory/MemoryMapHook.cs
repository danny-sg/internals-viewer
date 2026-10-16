using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal sealed class MemoryMapHook(IEnumerable<MemoryMapFunction> functions) : ITraceHook
{
    private const ulong SmallestAddress = 0x10000;

    private const int InstanceArgument = 0;

    public IEnumerable<TraceHookFunction> Functions
        => Operations.Select(o => new TraceHookFunction(o.Key, o.Value == MemoryMapOperation.Allocated
                                                                   ? TraceHookEvents.Return
                                                                   : TraceHookEvents.Entry));

    private Dictionary<ulong, MemoryMapOperation> Operations { get; }
        = functions.GroupBy(f => f.Address).ToDictionary(g => g.Key, g => g.Last().Operation);

    private List<AddressEntry> Entries { get; } = [];

    private Dictionary<ulong, int> LatestEntries { get; } = [];

    private Dictionary<ulong, List<int>> OwnedEntries { get; } = [];

    public AddressEntry[] Build() => [.. Entries];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (Operations.TryGetValue(call.Function, out var operation)
            && operation is MemoryMapOperation.Constructed or MemoryMapOperation.Visited)
        {
            Record(operation, call.Arguments[InstanceArgument], call, replay);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Operations.TryGetValue(call.Function, out var operation) && operation == MemoryMapOperation.Allocated)
        {
            Record(operation, call.ReturnValue, call, replay);
        }
    }

    public void OnFrameEnded(in TraceHookCall call)
    {
        if (!OwnedEntries.Remove(call.Id, out var owned))
        {
            return;
        }

        foreach (var index in owned)
        {
            Entries[index] = Entries[index] with { ToPosition = call.Position };
        }
    }

    private static ulong GetOwner(ReadOnlySpan<TraceHookFrame> frames, ulong address)
    {
        foreach (var frame in frames)
        {
            if (frame.StackPointer > address)
            {
                return frame.Id;
            }
        }

        return 0;
    }

    private static bool IsActive(ReadOnlySpan<TraceHookFrame> frames, ulong call)
    {
        foreach (var frame in frames)
        {
            if (frame.Id == call)
            {
                return true;
            }
        }

        return false;
    }

    private void Record(MemoryMapOperation operation, ulong address, in TraceHookCall call, ITraceReplay replay)
    {
        if (address < SmallestAddress)
        {
            return;
        }

        if (LatestEntries.TryGetValue(address, out var latest) && Entries[latest].IsAlive)
        {
            var entry = Entries[latest];

            var existing = entry.Operation;

            if (operation == MemoryMapOperation.Visited && existing != MemoryMapOperation.Allocated)
            {
                return;
            }

            if (operation == MemoryMapOperation.Constructed
                && existing == MemoryMapOperation.Constructed
                && IsActive(replay.GetFrames(call.Thread), entry.Frame))
            {
                return;
            }

            if (operation == MemoryMapOperation.Allocated && existing == MemoryMapOperation.Allocated && entry.Frame > call.Id)
            {
                Entries[latest] = entry with { Frame = call.Id, Call = (int)call.Call, FromPosition = call.Position, Node = call.Node };

                return;
            }
        }

        var owner = address < call.StackPointer ? 0 : GetOwner(replay.GetFrames(call.Thread), address);

        LatestEntries[address] = Entries.Count;

        if (owner != 0)
        {
            if (!OwnedEntries.TryGetValue(owner, out var owned))
            {
                owned = [];

                OwnedEntries[owner] = owned;

                replay.WatchFrame(call.Thread, owner);
            }

            owned.Add(Entries.Count);
        }

        Entries.Add(new AddressEntry(address,
                                     call.Id,
                                     (int)call.Call,
                                     call.Position,
                                     owner,
                                     TracePosition.Unknown,
                                     call.Node,
                                     call.Thread,
                                     operation));
    }
}
