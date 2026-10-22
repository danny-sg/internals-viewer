using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

internal sealed class MemoryMapHook(IEnumerable<MemoryMapFunction> functions, IReadOnlyDictionary<ulong, string>? vtables = null)
    : ITraceHook
{
    private const int InstanceArgument = 0;

    public IEnumerable<TraceHookFunction> Functions => Operations.Select(o => new TraceHookFunction(o.Key, GetEvents(o.Value)));

    private Dictionary<ulong, MemoryMapFunction> Operations { get; } = GetOperations(functions);

    private IReadOnlyDictionary<ulong, string> Vtables { get; } = vtables ?? new Dictionary<ulong, string>();

    private ChunkedList<AddressEntry> Entries { get; } = new();

    private Dictionary<ulong, int> LatestEntries { get; } = [];

    private Dictionary<ulong, List<int>> OwnedEntries { get; } = [];

    private HashSet<int> ReadEntries { get; } = [];

    public ChunkedList<AddressEntry> Build() => Entries;

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Operations.TryGetValue(call.FunctionAddress, out var function))
        {
            return;
        }

        foreach (var argument in call.Arguments)
        {
            ReadVtable(argument, replay);
        }

        if (function.Operation is MemoryMapOperation.Constructed or MemoryMapOperation.Visited)
        {
            Record(function.Operation, call.Arguments[InstanceArgument], call, replay);
        }

        foreach (var declared in function.Declared)
        {
            if (declared.Slot < ArgumentLayout.RegisterCount)
            {
                Record(MemoryMapOperation.Declared, call.Arguments[declared.Slot], call, replay, declared.Name);
            }
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Operations.TryGetValue(call.FunctionAddress, out var function))
        {
            return;
        }

        if (function.Operation == MemoryMapOperation.Allocated)
        {
            Record(MemoryMapOperation.Allocated, call.ReturnValue, call, replay);
        }

        foreach (var declared in function.Declared)
        {
            if (declared.Slot == ArgumentLayout.ReturnRegister)
            {
                Record(MemoryMapOperation.Declared, call.ReturnValue, call, replay, declared.Name);
            }
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

    private static Dictionary<ulong, MemoryMapFunction> GetOperations(IEnumerable<MemoryMapFunction> functions)
    {
        var operations = functions.TryGetNonEnumeratedCount(out var count)
            ? new Dictionary<ulong, MemoryMapFunction>(count)
            : new Dictionary<ulong, MemoryMapFunction>();

        foreach (var function in functions)
        {
            ref var operation = ref CollectionsMarshal.GetValueRefOrAddDefault(operations, function.Address, out var exists);

            operation = exists ? function with { Declared = [.. operation.Declared.Union(function.Declared)] } : function;
        }

        return operations;
    }

    private static TraceHookEvents GetEvents(MemoryMapFunction function)
    {
        var events = function.Operation switch
        {
            MemoryMapOperation.Allocated => TraceHookEvents.Return,
            MemoryMapOperation.Constructed or MemoryMapOperation.Visited => TraceHookEvents.Entry,
            _ => TraceHookEvents.None
        };

        foreach (var declared in function.Declared)
        {
            events |= declared.Slot == ArgumentLayout.ReturnRegister ? TraceHookEvents.Return : TraceHookEvents.Entry;
        }

        return events;
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

    private void ReadVtable(ulong address, ITraceReplay replay)
    {
        if (Vtables.Count == 0
            || address < TraceAddress.MinAddress
            || !LatestEntries.TryGetValue(address, out var latest)
            || Entries[latest] is not { Operation: MemoryMapOperation.Allocated, ClassName: null, IsAlive: true } entry
            || !ReadEntries.Add(latest))
        {
            return;
        }

        if (replay.TryRead(address, out ulong vtable) && Vtables.TryGetValue(vtable, out var className))
        {
            Entries[latest] = entry with { ClassName = className };
        }
    }

    private void Record(MemoryMapOperation operation,
                        ulong address,
                        in TraceHookCall call,
                        ITraceReplay replay,
                        string? className = null)
    {
        if (address < TraceAddress.MinAddress)
        {
            return;
        }

        if (LatestEntries.TryGetValue(address, out var latest) && Entries[latest].IsAlive)
        {
            var entry = Entries[latest];

            var existing = entry.Operation;

            var isNamedAllocation = existing == MemoryMapOperation.Allocated && entry.ClassName is not null;

            if (operation == MemoryMapOperation.Visited
                && (isNamedAllocation || existing is not (MemoryMapOperation.Allocated or MemoryMapOperation.Declared)))
            {
                return;
            }

            if (operation == MemoryMapOperation.Declared && (isNamedAllocation || existing != MemoryMapOperation.Allocated))
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
                Entries[latest] = entry with
                {
                    Frame = call.Id,
                    CallIndex = (int)call.CallIndex,
                    FromPosition = call.Position,
                    CallNodeIndex = call.CallNodeIndex
                };

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
                                     (int)call.CallIndex,
                                     owner,
                                     call.Position,
                                     TracePosition.Unknown,
                                     call.CallNodeIndex,
                                     call.Thread,
                                     operation,
                                     className));
    }
}
