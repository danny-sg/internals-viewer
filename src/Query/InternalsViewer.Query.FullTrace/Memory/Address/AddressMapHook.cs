using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

internal sealed class AddressMapHook(Dictionary<ulong, AddressMapFunction> operations,
                                    IEnumerable<AddressMapFunction> allocations,
                                    IReadOnlyDictionary<ulong, string>? vtables = null)
    : ITraceHook
{
    private const int InstanceArgument = 0;

    public IEnumerable<TraceHookFunction> Functions
        => Operations.Where(o => IsMapped(o.Value) && !Allocations.ContainsKey(o.Key))
                     .Concat(Allocations)
                     .Select(o => new TraceHookFunction(o.Key, GetEvents(o.Value)));

    private Dictionary<ulong, AddressMapFunction> Operations { get; } = operations;

    private Dictionary<ulong, AddressMapFunction> Allocations { get; } = GetAllocations(operations, allocations);

    private IReadOnlyDictionary<ulong, string> Vtables { get; } = vtables ?? new Dictionary<ulong, string>();

    private ChunkedList<AddressEntry> Entries { get; } = [];

    private Dictionary<ulong, int> LatestEntries { get; } = [];

    private Dictionary<ulong, List<int>> OwnedEntries { get; } = [];

    private HashSet<int> ReadEntries { get; } = [];

    private ListPool<int> Lists { get; } = new();

    public ChunkedList<AddressEntry> Build() => Entries;

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!TryGetFunction(call.FunctionAddress, out var function))
        {
            return;
        }

        foreach (var argument in call.Arguments)
        {
            ReadVtable(argument, replay);
        }

        if (function.Operation is AddressMapOperation.Constructed or AddressMapOperation.Visited)
        {
            Record(function.Operation, call.Arguments[InstanceArgument], call, replay);
        }

        foreach (var declared in function.DeclaredClasses)
        {
            if (declared.ArgumentSlot < ArgumentLayout.RegisterCount)
            {
                Record(AddressMapOperation.Declared, call.Arguments[declared.ArgumentSlot], call, replay, declared.Name);
            }
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!TryGetFunction(call.FunctionAddress, out var function))
        {
            return;
        }

        if (function.Operation == AddressMapOperation.Allocated)
        {
            Record(AddressMapOperation.Allocated, call.ReturnValue, call, replay);
        }

        foreach (var declared in function.DeclaredClasses)
        {
            if (declared.ArgumentSlot == ArgumentLayout.ReturnRegister)
            {
                Record(AddressMapOperation.Declared, call.ReturnValue, call, replay, declared.Name);
            }
        }
    }

    public void OnFrameEnded(in TraceHookCall call)
    {
        if (!OwnedEntries.Remove(call.CallId, out var owned))
        {
            return;
        }

        foreach (var index in owned)
        {
            Entries[index] = Entries[index] with { ToPosition = call.Position };
        }

        Lists.Return(owned);
    }

    internal static Dictionary<ulong, AddressMapFunction> GetOperations(IEnumerable<AddressMapFunction> functions)
    {
        var operations = functions.TryGetNonEnumeratedCount(out var count)
            ? new Dictionary<ulong, AddressMapFunction>(count)
            : new Dictionary<ulong, AddressMapFunction>();

        foreach (var function in functions)
        {
            ref var operation = ref CollectionsMarshal.GetValueRefOrAddDefault(operations, function.Address, out var exists);

            operation = exists
                ? function with
                {
                    DeclaredClasses = [.. operation.DeclaredClasses.Union(function.DeclaredClasses)],
                    Pointers = [.. operation.Pointers.Union(function.Pointers)]
                }
                : function;
        }

        return operations;
    }

    private static TraceHookEvents GetEvents(AddressMapFunction function)
    {
        var events = function.Operation switch
        {
            AddressMapOperation.Allocated => TraceHookEvents.Return,
            AddressMapOperation.Constructed or AddressMapOperation.Visited => TraceHookEvents.Entry,
            _ => TraceHookEvents.None
        };

        foreach (var declared in function.DeclaredClasses)
        {
            events |= declared.ArgumentSlot == ArgumentLayout.ReturnRegister ? TraceHookEvents.Return : TraceHookEvents.Entry;
        }

        return events;
    }

    private static Dictionary<ulong, AddressMapFunction> GetAllocations(Dictionary<ulong, AddressMapFunction> operations,
                                                                       IEnumerable<AddressMapFunction> allocations)
    {
        var merged = new Dictionary<ulong, AddressMapFunction>();

        foreach (var allocation in allocations)
        {
            merged[allocation.Address] = operations.TryGetValue(allocation.Address, out var operation)
                ? allocation with
                {
                    DeclaredClasses = [.. operation.DeclaredClasses.Union(allocation.DeclaredClasses)],
                    Pointers = [.. operation.Pointers.Union(allocation.Pointers)]
                }
                : allocation;
        }

        return merged;
    }

    private static bool IsMapped(AddressMapFunction function)
        => function.Operation != AddressMapOperation.Declared || function.DeclaredClasses.Length > 0;

    private static ulong GetOwner(ReadOnlySpan<TraceHookFrame> frames, ulong address)
        => TraceHookFrameLookup.GetOwnerIndex(frames, address) is >= 0 and var index ? frames[index].CallId : 0;

    private static bool IsActive(ReadOnlySpan<TraceHookFrame> frames, ulong call)
    {
        foreach (var frame in frames)
        {
            if (frame.CallId == call)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetFunction(ulong address, out AddressMapFunction function)
        => Allocations.TryGetValue(address, out function) || (Operations.TryGetValue(address, out function) && IsMapped(function));

    private void ReadVtable(ulong address, ITraceReplay replay)
    {
        if (Vtables.Count == 0
            || address < TraceAddress.MinAddress
            || !LatestEntries.TryGetValue(address, out var latest)
            || Entries[latest] is not { Operation: AddressMapOperation.Allocated, ClassName: null, IsAlive: true } entry
            || !ReadEntries.Add(latest))
        {
            return;
        }

        if (replay.TryRead(address, out ulong vtable) && Vtables.TryGetValue(vtable, out var className))
        {
            Entries[latest] = entry with { ClassName = className };
        }
    }

    private void Record(AddressMapOperation operation,
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

            var isNamedAllocation = existing == AddressMapOperation.Allocated && entry.ClassName is not null;

            if (operation == AddressMapOperation.Visited
                && (isNamedAllocation || existing is not (AddressMapOperation.Allocated or AddressMapOperation.Declared)))
            {
                return;
            }

            if (operation == AddressMapOperation.Declared && (isNamedAllocation || existing != AddressMapOperation.Allocated))
            {
                return;
            }

            if (operation == AddressMapOperation.Constructed
                && existing == AddressMapOperation.Constructed
                && IsActive(replay.GetFrames(call.Thread), entry.Frame))
            {
                return;
            }

            if (operation == AddressMapOperation.Allocated && existing == AddressMapOperation.Allocated && entry.Frame > call.CallId)
            {
                Entries[latest] = entry with
                {
                    Frame = call.CallId,
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
                owned = Lists.Rent();

                OwnedEntries[owner] = owned;

                replay.WatchFrame(call.Thread, owner);
            }

            owned.Add(Entries.Count);
        }

        Entries.Add(new AddressEntry(address,
                                     call.CallId,
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
