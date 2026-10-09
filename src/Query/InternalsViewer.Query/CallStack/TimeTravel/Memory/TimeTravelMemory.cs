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
        var byAddress = functions.ToDictionary(f => f.Address);

        var calls = new TimeTravelCallListCache(log);

        var scanned = timeline is null ? new ScannedCalls() : ScanSpans(calls, byAddress, timeline, clerks ?? MemoryClerkSnapshot.Empty);

        var plumbing = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var values = new Dictionary<ulong, CallValues>();

        var events = new List<MemoryEvent>();

        foreach (var function in functions.Where(f => f.Allocates || f is { Operation: MemoryOperation.Free, PointerSlot: >= 0 }))
        {
            if (calls.Of(function.Address) is not { } logged)
            {
                continue;
            }

            var callValues = new CallValues(function, logged.Count);

            var positions = scanned.Positions.GetValueOrDefault(function.Address);

            var kinds = scanned.Kinds.GetValueOrDefault(function.Address);

            for (var index = 0; index < logged.Count; index++)
            {
                var call = logged[index];

                var node = log.NodeOf(call);

                if (!function.Applies(call) || (function.IsPage && IsPlumbing(node, plumbing)))
                {
                    continue;
                }

                var position = PositionOf(positions, index, call);

                var pointer = function.PointerOf(call);

                if (!function.Allocates)
                {
                    callValues.Released[index] = pointer;

                    if (pointer != 0)
                    {
                        events.Add(FreeEvent(position, node, pointer));
                    }

                    continue;
                }

                callValues.Bytes[index] = function.BytesOf(call);

                callValues.Returned[index] = call.ReturnedValue;

                if (function.Operation == MemoryOperation.Reallocate && pointer != 0)
                {
                    callValues.Released[index] = pointer;

                    events.Add(FreeEvent(position, node, pointer));
                }

                events.Add(new MemoryEvent(position,
                                           MemoryOperation.Allocate,
                                           node,
                                           callValues.Bytes[index],
                                           call.ReturnedValue,
                                           call.Returned,
                                           KindOf(kinds?[index], function)));
            }

            values[function.Address] = callValues;
        }

        if (timeline is not null)
        {
            var (allocations, frees) = scanned.MemoryOf(values);

            timeline.SetMemory(allocations, frees);
        }

        return Summarise(events.OrderBy(e => e.Position).ThenBy(e => e.Operation == MemoryOperation.Free ? 0 : 1), byAddress, operators);
    }

    private static TimeTravelMemorySummary Summarise(IEnumerable<MemoryEvent> events,
                                                     Dictionary<ulong, MemoryFunction> functions,
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
                purpose = IsNested(node, functions) ? null : PurposeOf(node);

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

    private static ScannedCalls ScanSpans(TimeTravelCallListCache calls,
                                          Dictionary<ulong, MemoryFunction> functions,
                                          TimeTravelTimeline timeline,
                                          MemoryClerkSnapshot snapshot)
    {
        var scanned = new ScannedCalls();

        var threads = new Dictionary<uint, ThreadCalls>();

        foreach (var span in timeline.ResolvedSpans())
        {
            if (span.Node.Frame is not { } frame
                || !functions.TryGetValue(frame.Address, out var function)
                || span.Call is not (>= 0 and var call)
                || calls.Of(function.Address) is not { } logged
                || call >= logged.Count)
            {
                continue;
            }

            var start = span.StartOf(TimeTravelTimelineAxis.Position);

            var end = span.EndOf(TimeTravelTimelineAxis.Position);

            if (!scanned.Positions.TryGetValue(function.Address, out var positions))
            {
                positions = new double[logged.Count];

                Array.Fill(positions, double.NaN);

                scanned.Positions[function.Address] = positions;
            }

            positions[call] = start;

            if (!threads.TryGetValue(span.Thread.ThreadId, out var thread))
            {
                thread = new ThreadCalls(span.Thread.ThreadId);

                threads[thread.ThreadId] = thread;

                scanned.Threads.Add(thread);
            }

            thread.Calls.Add((function.Address, call, start, end));

            if (!function.CarriesClerk && !function.IsObjectCall)
            {
                continue;
            }

            var arguments = logged[call];

            if (function.CarriesClerk)
            {
                thread.Pages.Add((start, arguments.Slot(0)));
            }

            if (function.IsObjectCall && function.ObjectOf(arguments) is not 0 and var memoryObject)
            {
                thread.Objects.Add((start, end, memoryObject));
            }
        }

        var objectClerks = new Dictionary<ulong, ulong>();

        var objectParents = new Dictionary<ulong, ulong>();

        foreach (var thread in scanned.Threads)
        {
            thread.Sort();

            foreach (var (start, end, memoryObject) in thread.Objects)
            {
                if (FirstWithin(thread.PageStarts, start, end) is >= 0 and var page)
                {
                    objectClerks.TryAdd(memoryObject, thread.Pages[page].Clerk);
                }

                if (FirstWithin(thread.ObjectStarts, Math.BitIncrement(start), end) is >= 0 and var nested
                    && thread.Objects[nested].Object != memoryObject)
                {
                    objectParents.TryAdd(memoryObject, thread.Objects[nested].Object);
                }
            }
        }

        foreach (var thread in scanned.Threads)
        {
            foreach (var (address, call, start, end) in thread.Calls)
            {
                if (!functions[address].Allocates)
                {
                    continue;
                }

                var kind = FirstWithin(thread.PageStarts, start, end) is >= 0 and var page
                    ? ClerkType(snapshot, thread.Pages[page].Clerk)
                    : null;

                if (kind is null && FirstWithin(thread.ObjectStarts, start, end) is >= 0 and var inner)
                {
                    kind = ObjectType(thread.Objects[inner].Object, objectClerks, objectParents, snapshot);
                }

                if (kind is null)
                {
                    continue;
                }

                if (!scanned.Kinds.TryGetValue(address, out var kinds))
                {
                    kinds = new string?[scanned.Positions[address].Length];

                    scanned.Kinds[address] = kinds;
                }

                kinds[call] = kind;
            }
        }

        return scanned;
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

    private static int FirstWithin(double[] starts, double start, double end)
    {
        var index = SortedSearch.FirstAtOrAfter(starts, start);

        return index < starts.Length && starts[index] < end ? index : -1;
    }

    private static string KindOf(string? kind, MemoryFunction function)
        => kind ?? (function.IsHeap ? Heap : function.IsVirtual ? VirtualMemory : Unattributed);

    private static MemoryEvent FreeEvent(double position, CallStackNode? node, ulong pointer)
        => new(position, MemoryOperation.Free, node, 0, pointer, false, string.Empty);

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

    private static bool IsNested(CallStackNode node, Dictionary<ulong, MemoryFunction> functions)
        => node.Ancestors()
               .Skip(1)
               .Any(a => a.Frame is { } frame && functions.TryGetValue(frame.Address, out var function) && function.Allocates);

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

    private sealed class CallValues(MemoryFunction function, int count)
    {
        public ulong[] Bytes { get; } = function.Allocates ? new ulong[count] : [];

        public ulong[] Returned { get; } = function.Allocates ? new ulong[count] : [];

        public ulong[] Released { get; } = function.Operation is MemoryOperation.Free or MemoryOperation.Reallocate
            ? new ulong[count]
            : [];
    }

    private sealed class ScannedCalls
    {
        public Dictionary<ulong, double[]> Positions { get; } = [];

        public Dictionary<ulong, string?[]> Kinds { get; } = [];

        public List<ThreadCalls> Threads { get; } = [];

        public (List<TimeTravelAllocation> Allocations, List<TimeTravelFree> Frees) MemoryOf(Dictionary<ulong, CallValues> values)
        {
            var allocations = new List<TimeTravelAllocation>();

            var frees = new List<TimeTravelFree>();

            foreach (var thread in Threads)
            {
                foreach (var (address, call, start, end) in thread.Calls)
                {
                    if (!values.TryGetValue(address, out var callValues))
                    {
                        continue;
                    }

                    if (call < callValues.Bytes.Length && callValues.Bytes[call] > 0)
                    {
                        allocations.Add(new TimeTravelAllocation(thread.ThreadId,
                                                                 start,
                                                                 end,
                                                                 callValues.Bytes[call],
                                                                 callValues.Returned[call]));
                    }

                    if (call < callValues.Released.Length && callValues.Released[call] != 0)
                    {
                        frees.Add(new TimeTravelFree(thread.ThreadId, start, end, callValues.Released[call]));
                    }
                }
            }

            return (allocations, frees);
        }
    }

    private sealed class ThreadCalls(uint threadId)
    {
        public uint ThreadId { get; } = threadId;

        public List<(ulong Address, int Call, double Start, double End)> Calls { get; } = [];

        public List<(double Start, ulong Clerk)> Pages { get; } = [];

        public List<(double Start, double End, ulong Object)> Objects { get; } = [];

        public double[] PageStarts { get; private set; } = [];

        public double[] ObjectStarts { get; private set; } = [];

        public void Sort()
        {
            Pages.Sort();

            Objects.Sort();

            PageStarts = [.. Pages.Select(p => p.Start)];

            ObjectStarts = [.. Objects.Select(o => o.Start)];
        }
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
