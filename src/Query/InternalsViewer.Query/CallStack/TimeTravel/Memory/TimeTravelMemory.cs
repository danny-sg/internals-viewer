using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public static class TimeTravelMemory
{
    private const string NoOperator = "No Operator";

    private const string BufferPool = "Buffer Pool";

    private const string Compilation = "Compilation";

    private const string QueryProfiling = "Query Profiling";

    private const string ProfilingClassPrefix = "CProfile";

    private const int MaximumObjectDepth = 16;

    private const string Other = "Other";

    private const string SqlOsModule = "sqldk";

    public const string UnknownClerk = "Unknown Clerk";

    private const string Heap = "Heap";

    public const string Unattributed = "Unattributed";

    private const string VirtualMemory = "Virtual Memory";

    public static TimeTravelMemorySummary Apply(TimeTravelCallLog log,
                                                IReadOnlyList<MemoryFunction> functions,
                                                TimeTravelTimeline? timeline,
                                                IReadOnlyList<ExecutionOperatorEvent> operators,
                                                MemoryClerkSnapshot? clerks = null)
    {
        var allocators = functions.Where(f => f.Allocates).ToDictionary(f => f.Address);

        var (kinds, positions) = timeline is null ? ([], []) : ScanSpans(log, functions, timeline, clerks ?? MemoryClerkSnapshot.Empty);

        var plumbing = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var sizes = new Dictionary<ulong, ulong[]>();

        var returned = new Dictionary<ulong, ulong[]>();

        var released = new Dictionary<ulong, ulong[]>();

        var events = new List<MemoryEvent>();

        foreach (var (address, function) in allocators)
        {
            if (log.CallsOf(address, 0) is not { } calls)
            {
                continue;
            }

            var bytes = new ulong[calls.Count];

            var pointers = new ulong[calls.Count];

            var previousPointers = new ulong[calls.Count];

            var callKinds = kinds.GetValueOrDefault(address);

            var callPositions = positions.GetValueOrDefault(address);

            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];

                var node = log.NodeOf(call);

                if (!function.Applies(call) || (function.IsPage && IsPlumbing(node, plumbing)))
                {
                    continue;
                }

                var position = PositionOf(callPositions, index, call);

                bytes[index] = function.BytesOf(call);

                pointers[index] = call.Returned ? call.ReturnValue : 0;

                if (function.Operation == MemoryOperation.Reallocate && PointerOf(function, call) is not 0 and var previous)
                {
                    previousPointers[index] = previous;

                    events.Add(new MemoryEvent(position, MemoryOperation.Free, node, 0, previous, false, string.Empty));
                }

                events.Add(new MemoryEvent(position,
                                           MemoryOperation.Allocate,
                                           node,
                                           bytes[index],
                                           call.Returned ? call.ReturnValue : 0,
                                           call.Returned,
                                           KindOf(callKinds?[index], function)));
            }

            sizes[address] = bytes;

            returned[address] = pointers;

            if (function.Operation == MemoryOperation.Reallocate)
            {
                released[address] = previousPointers;
            }
        }

        foreach (var function in functions.Where(f => f.Operation == MemoryOperation.Free && f.PointerSlot >= 0))
        {
            if (log.CallsOf(function.Address, 0) is not { } calls)
            {
                continue;
            }

            var pointers = new ulong[calls.Count];

            var callPositions = positions.GetValueOrDefault(function.Address);

            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];

                var node = log.NodeOf(call);

                if (!function.Applies(call) || (function.IsPage && IsPlumbing(node, plumbing)))
                {
                    continue;
                }

                pointers[index] = PointerOf(function, call);

                if (pointers[index] != 0)
                {
                    events.Add(new MemoryEvent(PositionOf(callPositions, index, call),
                                               MemoryOperation.Free,
                                               node,
                                               0,
                                               pointers[index],
                                               false,
                                               string.Empty));
                }
            }

            released[function.Address] = pointers;
        }

        if (timeline is not null)
        {
            var (allocations, frees) = MemoryEventsOf(timeline, sizes, returned, released);

            timeline.SetMemory(allocations, frees);
        }

        return Summarise(events.OrderBy(e => e.Position).ThenBy(e => e.Operation == MemoryOperation.Free ? 0 : 1), allocators, operators);
    }

    private static TimeTravelMemorySummary Summarise(IEnumerable<MemoryEvent> events,
                                                     Dictionary<ulong, MemoryFunction> allocators,
                                                     IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var purposes = new Dictionary<string, PurposeTotals>();

        var purposeOf = new Dictionary<CallStackNode, string?>(ReferenceEqualityComparer.Instance);

        var operatorsByFrame = OwnersByFrame(operators.Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 }));

        var statementsByFrame = OwnersByFrame(operators.Where(o => o.PlanNodeIdentifier is { NodeId: < 0 }));

        var ownersOf = new Dictionary<CallStackNode, (ExecutionOperatorEvent? Operator, ExecutionOperatorEvent? Statement)>(
            ReferenceEqualityComparer.Instance);

        var byOperator = new Dictionary<ExecutionOperatorEvent, PurposeTotals>(ReferenceEqualityComparer.Instance);

        var kinds = new Dictionary<string, PurposeTotals>();

        var live = new Dictionary<ulong, LiveAllocation>();

        ulong allocated = 0;

        ulong inUse = 0;

        ulong peak = 0;

        long allocations = 0;

        long returned = 0;

        long frees = 0;

        long matched = 0;

        ulong operatorBytes = 0;

        ulong statementBytes = 0;

        ulong outsideBytes = 0;

        foreach (var memoryEvent in events)
        {
            if (memoryEvent.Operation == MemoryOperation.Free)
            {
                frees++;

                if (live.Remove(memoryEvent.Pointer, out var freed))
                {
                    matched++;

                    inUse -= freed.Bytes;

                    freed.Purpose.Free(freed.Bytes, freed.KindName);

                    freed.Operator?.Free(freed.Bytes, freed.KindName);

                    freed.Statement?.Free(freed.Bytes, freed.KindName);

                    freed.Kind.Free(freed.Bytes, null);
                }

                continue;
            }

            if (memoryEvent.Node is not { } node)
            {
                continue;
            }

            if (!purposeOf.TryGetValue(node, out var purpose))
            {
                purpose = IsNested(node, allocators) ? null : PurposeOf(node);

                purposeOf[node] = purpose;
            }

            if (purpose is null)
            {
                continue;
            }

            node.AllocatedBytes += (long)memoryEvent.Bytes;

            node.Allocations++;

            allocated += memoryEvent.Bytes;

            allocations++;

            var totals = TotalsOf(purposes, purpose);

            totals.Allocate(node, memoryEvent.Bytes, memoryEvent.Kind);

            var kindTotals = TotalsOf(kinds, memoryEvent.Kind);

            kindTotals.Allocate(node, memoryEvent.Bytes, null);

            if (!ownersOf.TryGetValue(node, out var owners))
            {
                owners = (OwnerOf(node, operatorsByFrame), OwnerOf(node, statementsByFrame));

                ownersOf[node] = owners;
            }

            if (owners.Operator is not null)
            {
                operatorBytes += memoryEvent.Bytes;
            }
            else if (owners.Statement is not null)
            {
                statementBytes += memoryEvent.Bytes;
            }
            else
            {
                outsideBytes += memoryEvent.Bytes;
            }

            var operatorTotals = owners.Operator is null ? null : TotalsOf(byOperator, owners.Operator);

            var statementTotals = owners.Statement is null ? null : TotalsOf(byOperator, owners.Statement);

            operatorTotals?.Allocate(node, memoryEvent.Bytes, memoryEvent.Kind);

            statementTotals?.Allocate(node, memoryEvent.Bytes, memoryEvent.Kind);

            if (!memoryEvent.Returned)
            {
                continue;
            }

            returned++;

            if (memoryEvent.Pointer != 0)
            {
                live[memoryEvent.Pointer] = new LiveAllocation(totals,
                                                               operatorTotals,
                                                               statementTotals,
                                                               kindTotals,
                                                               memoryEvent.Kind,
                                                               memoryEvent.Bytes);

                inUse += memoryEvent.Bytes;

                peak = Math.Max(peak, inUse);
            }
        }

        foreach (var operatorEvent in operators)
        {
            operatorEvent.Memory = byOperator.TryGetValue(operatorEvent, out var ownerTotals)
                ? ownerTotals.ToPurpose(operatorEvent.OperatorDescription)
                : null;
        }

        return new TimeTravelMemorySummary(allocated,
                                           allocations,
                                           returned,
                                           frees,
                                           matched,
                                           peak,
                                           [.. purposes.OrderByDescending(p => p.Value.Allocated).Select(p => p.Value.ToPurpose(p.Key))],
                                           [.. kinds.OrderByDescending(k => k.Value.Allocated).Select(k => k.Value.ToPurpose(k.Key))],
                                           operatorBytes,
                                           statementBytes,
                                           outsideBytes);
    }

    private static PurposeTotals TotalsOf<TKey>(Dictionary<TKey, PurposeTotals> totals, TKey key) where TKey : notnull
    {
        if (!totals.TryGetValue(key, out var keyTotals))
        {
            keyTotals = new PurposeTotals();

            totals[key] = keyTotals;
        }

        return keyTotals;
    }

    private static Dictionary<CallStackNode, List<ExecutionOperatorEvent>> OwnersByFrame(IEnumerable<ExecutionOperatorEvent> operators)
    {
        var ownersByFrame = new Dictionary<CallStackNode, List<ExecutionOperatorEvent>>(ReferenceEqualityComparer.Instance);

        foreach (var operatorEvent in operators)
        {
            foreach (var frame in operatorEvent.EntryFrames)
            {
                if (!ownersByFrame.TryGetValue(frame, out var owners))
                {
                    owners = [];

                    ownersByFrame[frame] = owners;
                }

                owners.Add(operatorEvent);
            }
        }

        return ownersByFrame;
    }

    private static ExecutionOperatorEvent? OwnerOf(CallStackNode node,
                                                   Dictionary<CallStackNode, List<ExecutionOperatorEvent>> ownersByFrame)
    {
        foreach (var frame in node.Ancestors())
        {
            if (ownersByFrame.TryGetValue(frame, out var owners))
            {
                return owners.Count == 1 ? owners[0] : null;
            }
        }

        return null;
    }

    private static (Dictionary<ulong, string?[]> Kinds, Dictionary<ulong, double[]> Positions) ScanSpans(
        TimeTravelCallLog log,
        IReadOnlyList<MemoryFunction> functions,
        TimeTravelTimeline timeline,
        MemoryClerkSnapshot snapshot)
    {
        var byAddress = functions.ToDictionary(f => f.Address);

        var calls = new Dictionary<ulong, TimeTravelCallList?>();

        var positions = new Dictionary<ulong, double[]>();

        var threads = new List<ThreadCalls>();

        var objectClerks = new Dictionary<ulong, ulong>();

        foreach (var thread in timeline.Threads)
        {
            var found = new ThreadCalls();

            foreach (var row in thread.Rows)
            {
                var starts = row.Starts(TimeTravelTimelineAxis.Position);

                var ends = row.Ends(TimeTravelTimelineAxis.Position);

                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame
                        || !byAddress.TryGetValue(frame.Address, out var function)
                        || row.CallAt(index) is not (>= 0 and var call)
                        || CallsOf(log, calls, function.Address) is not { } logged
                        || call >= logged.Count)
                    {
                        continue;
                    }

                    if (!positions.TryGetValue(function.Address, out var callPositions))
                    {
                        callPositions = new double[logged.Count];

                        Array.Fill(callPositions, double.NaN);

                        positions[function.Address] = callPositions;
                    }

                    callPositions[call] = starts[index];

                    if (function.CarriesClerk && logged[call].IntegerSlots.Length > 0)
                    {
                        found.Pages.Add((starts[index], logged[call].IntegerSlots[0]));
                    }

                    if (function.IsObjectCall && function.ObjectOf(logged[call]) is not 0 and var memoryObject)
                    {
                        found.Objects.Add((starts[index], ends[index], memoryObject));
                    }

                    if (function.Allocates)
                    {
                        found.Allocations.Add((function.Address, call, starts[index], ends[index]));
                    }
                }
            }

            found.Pages.Sort();

            found.Objects.Sort();

            threads.Add(found);
        }

        var objectParents = new Dictionary<ulong, ulong>();

        foreach (var thread in threads)
        {
            var pageStarts = thread.Pages.Select(p => p.Start).ToArray();

            var objectStarts = thread.Objects.Select(o => o.Start).ToArray();

            foreach (var (start, end, memoryObject) in thread.Objects)
            {
                if (FirstWithin(pageStarts, start, end) is >= 0 and var page)
                {
                    objectClerks.TryAdd(memoryObject, thread.Pages[page].Clerk);
                }

                if (FirstWithin(objectStarts, Math.BitIncrement(start), end) is >= 0 and var nested
                    && thread.Objects[nested].Object != memoryObject)
                {
                    objectParents.TryAdd(memoryObject, thread.Objects[nested].Object);
                }
            }
        }

        var kinds = new Dictionary<ulong, string?[]>();

        foreach (var thread in threads)
        {
            var pageStarts = thread.Pages.Select(p => p.Start).ToArray();

            var objectStarts = thread.Objects.Select(o => o.Start).ToArray();

            foreach (var (address, call, start, end) in thread.Allocations)
            {
                var kind = FirstWithin(pageStarts, start, end) is >= 0 and var page ? ClerkType(snapshot, thread.Pages[page].Clerk) : null;

                if (kind is null && FirstWithin(objectStarts, start, end) is >= 0 and var inner)
                {
                    kind = ObjectType(thread.Objects[inner].Object, objectClerks, objectParents, snapshot);
                }

                if (kind is null || CallsOf(log, calls, address) is not { } logged)
                {
                    continue;
                }

                if (!kinds.TryGetValue(address, out var callKinds))
                {
                    callKinds = new string?[logged.Count];

                    kinds[address] = callKinds;
                }

                callKinds[call] = kind;
            }
        }

        return (kinds, positions);
    }

    private static string ClerkType(MemoryClerkSnapshot snapshot, ulong clerk)
        => snapshot.Clerks.GetValueOrDefault(clerk) ?? $"{UnknownClerk} 0x{clerk:X}";

    private static string? ObjectType(ulong memoryObject,
                                      Dictionary<ulong, ulong> objectClerks,
                                      Dictionary<ulong, ulong> objectParents,
                                      MemoryClerkSnapshot snapshot)
    {
        for (var depth = 0; depth < MaximumObjectDepth && memoryObject != 0; depth++)
        {
            if (objectClerks.TryGetValue(memoryObject, out var clerk))
            {
                return ClerkType(snapshot, clerk);
            }

            if (snapshot.Objects.TryGetValue(memoryObject, out var type))
            {
                return type;
            }

            memoryObject = objectParents.GetValueOrDefault(memoryObject);
        }

        return null;
    }

    private static double PositionOf(double[]? positions, int index, TimeTravelArgumentCall call)
        => positions is not null && index < positions.Length && !double.IsNaN(positions[index]) ? positions[index] : call.Sequence;

    private static TimeTravelCallList? CallsOf(TimeTravelCallLog log, Dictionary<ulong, TimeTravelCallList?> calls, ulong address)
    {
        if (!calls.TryGetValue(address, out var logged))
        {
            logged = log.CallsOf(address, 0);

            calls[address] = logged;
        }

        return logged;
    }

    private static int FirstWithin(double[] starts, double start, double end)
    {
        var index = Array.BinarySearch(starts, start);

        if (index < 0)
        {
            index = ~index;
        }
        else
        {
            while (index > 0 && starts[index - 1] == start)
            {
                index--;
            }
        }

        return index < starts.Length && starts[index] < end ? index : -1;
    }

    private static string KindOf(string? kind, MemoryFunction function)
        => kind ?? (function.IsHeap ? Heap : function.IsVirtual ? VirtualMemory : Unattributed);

    private static bool IsPlumbing(CallStackNode? node, Dictionary<CallStackNode, bool> plumbing)
    {
        if (node is null)
        {
            return false;
        }

        if (!plumbing.TryGetValue(node, out var inside))
        {
            inside = node.Ancestors().Skip(1).Any(a => MemoryFunction.IsMemoryObjectClass(a.Frame?.Resolved?.ClassName));

            plumbing[node] = inside;
        }

        return inside;
    }

    private static bool IsNested(CallStackNode node, Dictionary<ulong, MemoryFunction> allocators)
        => node.Ancestors().Skip(1).Any(a => a.Frame is { } frame && allocators.ContainsKey(frame.Address));

    private static bool IsBufferPool(CallStackNode node)
        => node.Frame?.Resolved?.SymbolCategory is SymbolCategory.BufferPool or SymbolCategory.BufferManager;

    private static bool IsMemoryPlumbing(CallStackNode node)
        => string.Equals(node.Frame?.Module, SqlOsModule, StringComparison.OrdinalIgnoreCase) || IsBufferPool(node);

    private static CallStackNode? CallerOf(CallStackNode node)
        => node.Ancestors().Skip(1).FirstOrDefault(a => !IsMemoryPlumbing(a)) ?? node.Parent;

    private static CallStackNode EntryOf(CallStackNode node, CallStackNode? caller)
    {
        var entry = node;

        while (entry.Parent is { IsRoot: false } parent && !ReferenceEquals(parent, caller))
        {
            entry = parent;
        }

        return entry;
    }

    private static string PurposeOf(CallStackNode node)
    {
        if (node.Ancestors().Skip(1).Any(IsQueryProfiling))
        {
            return QueryProfiling;
        }

        foreach (var ancestor in node.Ancestors().Skip(1))
        {
            if (ancestor.Operator is { } operatorName)
            {
                return operatorName;
            }

            if (ancestor.Frame?.Resolved?.SymbolCategory is SymbolCategory.Compilation
                                                         or SymbolCategory.Optimization
                                                         or SymbolCategory.QueryBinding)
            {
                return Compilation;
            }
        }

        if (node.Ancestors().Skip(1).Any(IsBufferPool))
        {
            return BufferPool;
        }

        return CallerOf(node)?.Frame?.Resolved?.SymbolMetadata?.Name ?? Other;
    }

    private static bool IsQueryProfiling(CallStackNode node)
        => node.Frame?.Resolved?.ClassName?.StartsWith(ProfilingClassPrefix, StringComparison.Ordinal) == true;

    private static ulong PointerOf(MemoryFunction function, TimeTravelArgumentCall call)
        => function.PointerSlot >= 0 && function.PointerSlot < call.IntegerSlots.Length ? call.IntegerSlots[function.PointerSlot] : 0;

    private static (List<TimeTravelAllocation> Allocations, List<TimeTravelFree> Frees) MemoryEventsOf(
        TimeTravelTimeline timeline,
        Dictionary<ulong, ulong[]> sizes,
        Dictionary<ulong, ulong[]> returned,
        Dictionary<ulong, ulong[]> released)
    {
        var allocations = new List<TimeTravelAllocation>();

        var frees = new List<TimeTravelFree>();

        foreach (var thread in timeline.Threads)
        {
            foreach (var row in thread.Rows)
            {
                var starts = row.Starts(TimeTravelTimelineAxis.Position);

                var ends = row.Ends(TimeTravelTimelineAxis.Position);

                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame || row.CallAt(index) is not (>= 0 and var call))
                    {
                        continue;
                    }

                    if (sizes.TryGetValue(frame.Address, out var bytes) && call < bytes.Length && bytes[call] > 0)
                    {
                        allocations.Add(new TimeTravelAllocation(thread.ThreadId,
                                                                 starts[index],
                                                                 ends[index],
                                                                 bytes[call],
                                                                 returned[frame.Address][call]));
                    }

                    if (released.TryGetValue(frame.Address, out var pointers) && call < pointers.Length && pointers[call] != 0)
                    {
                        frees.Add(new TimeTravelFree(thread.ThreadId, starts[index], ends[index], pointers[call]));
                    }
                }
            }
        }

        return (allocations, frees);
    }

    private readonly record struct MemoryEvent(double Position,
                                               MemoryOperation Operation,
                                               CallStackNode? Node,
                                               ulong Bytes,
                                               ulong Pointer,
                                               bool Returned,
                                               string Kind);

    private readonly record struct LiveAllocation(PurposeTotals Purpose,
                                                  PurposeTotals? Operator,
                                                  PurposeTotals? Statement,
                                                  PurposeTotals Kind,
                                                  string KindName,
                                                  ulong Bytes);

    private sealed class ThreadCalls
    {
        public List<(double Start, ulong Clerk)> Pages { get; } = [];

        public List<(double Start, double End, ulong Object)> Objects { get; } = [];

        public List<(ulong Address, int Call, double Start, double End)> Allocations { get; } = [];
    }

    private sealed class PurposeTotals
    {
        public ulong Allocated { get; private set; }

        private ulong InUse { get; set; }

        private ulong Peak { get; set; }

        private ulong Freed { get; set; }

        private long Count { get; set; }

        private Dictionary<(string Caller, string Allocator), (ulong Bytes, long Count)> Uses { get; } = [];

        private Dictionary<string, PurposeTotals> Kinds { get; } = [];

        public void Allocate(CallStackNode node, ulong bytes, string? kind)
        {
            if (kind is not null)
            {
                TotalsOf(Kinds, kind).Allocate(node, bytes, null);
            }

            Allocated += bytes;

            Count++;

            InUse += bytes;

            Peak = Math.Max(Peak, InUse);

            var caller = CallerOf(node);

            var key = (caller is { IsRoot: false } ? caller.Symbol : NoOperator, EntryOf(node, caller).Symbol);

            Uses[key] = Uses.TryGetValue(key, out var use) ? (use.Bytes + bytes, use.Count + 1) : (bytes, 1);
        }

        public void Free(ulong bytes, string? kind)
        {
            if (kind is not null && Kinds.TryGetValue(kind, out var kindTotals))
            {
                kindTotals.Free(bytes, null);
            }

            Freed += bytes;

            InUse -= Math.Min(InUse, bytes);
        }

        public TimeTravelMemoryPurpose ToPurpose(string name)
            => new(name,
                   Allocated,
                   Count,
                   Freed,
                   Peak,
                   [.. Uses.OrderByDescending(u => u.Value.Bytes)
                           .Select(u => new TimeTravelMemoryUse(u.Key.Caller, u.Key.Allocator, u.Value.Bytes, u.Value.Count))])
            {
                Kinds = [.. Kinds.OrderByDescending(k => k.Value.Peak).Select(k => k.Value.ToPurpose(k.Key))]
            };
    }
}
