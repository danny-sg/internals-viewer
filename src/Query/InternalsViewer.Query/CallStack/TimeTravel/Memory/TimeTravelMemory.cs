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

    private const string Other = "Other";

    private const string SqlOsModule = "sqldk";

    private const long MinimumCandidateCalls = 10;

    private static readonly string[] CandidateWords = ["Alloc", "Steal", "Reserve", "Grow", "Buffers"];

    public static TimeTravelMemorySummary Apply(TimeTravelCallLog log,
                                                IReadOnlyList<MemoryFunction> functions,
                                                TimeTravelTimeline? timeline,
                                                IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var allocators = functions.Where(f => f.Allocates).ToDictionary(f => f.Address);

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

            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];

                bytes[index] = function.BytesOf(call);

                pointers[index] = call.Returned ? call.ReturnValue : 0;

                var node = log.NodeOf(call);

                if (function.Operation == MemoryOperation.Reallocate && PointerOf(function, call) is not 0 and var previous)
                {
                    previousPointers[index] = previous;

                    events.Add(new MemoryEvent(call.Sequence, MemoryOperation.Free, node, 0, previous, false));
                }

                events.Add(new MemoryEvent(call.Sequence,
                                           MemoryOperation.Allocate,
                                           node,
                                           bytes[index],
                                           call.Returned ? call.ReturnValue : 0,
                                           call.Returned));
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

            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];

                pointers[index] = PointerOf(function, call);

                if (pointers[index] != 0)
                {
                    events.Add(new MemoryEvent(call.Sequence, MemoryOperation.Free, log.NodeOf(call), 0, pointers[index], false));
                }
            }

            released[function.Address] = pointers;
        }

        if (timeline is not null)
        {
            var (allocations, frees) = MemoryEventsOf(timeline, sizes, returned, released);

            timeline.SetMemory(allocations, frees);
        }

        return Summarise(events.OrderBy(e => e.Sequence), allocators, operators);
    }

    public static IReadOnlyList<(string Operator, string Function, long Calls)> Candidates(CallStackTree callStack,
                                                                                         IReadOnlyList<MemoryFunction> functions)
    {
        var classified = functions.Select(f => f.Address).ToHashSet();

        var candidates = new Dictionary<(string Operator, string Function), long>();

        foreach (var node in callStack.Nodes())
        {
            if (node.Frame is not { } frame
                || classified.Contains(frame.Address)
                || node.Calls < MinimumCandidateCalls
                || node.Ancestors().Skip(1).Any(a => a.Frame is { } ancestor && classified.Contains(ancestor.Address))
                || !CandidateWords.Any(w => node.Symbol.Contains(w, StringComparison.Ordinal))
                || node.Ancestors().Skip(1).FirstOrDefault(a => a.HasOperator)?.Operator is not { } operatorName)
            {
                continue;
            }

            var key = (operatorName, node.Symbol);

            candidates[key] = candidates.GetValueOrDefault(key) + node.Calls;
        }

        return [.. candidates.OrderByDescending(c => c.Value).Select(c => (c.Key.Operator, c.Key.Function, c.Value))];
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

        var live = new Dictionary<ulong, (PurposeTotals Purpose, PurposeTotals? Operator, PurposeTotals? Statement, ulong Bytes)>();

        ulong allocated = 0;

        ulong inUse = 0;

        ulong peak = 0;

        long allocations = 0;

        long returned = 0;

        long frees = 0;

        long matched = 0;

        foreach (var memoryEvent in events)
        {
            if (memoryEvent.Operation == MemoryOperation.Free)
            {
                frees++;

                if (live.Remove(memoryEvent.Pointer, out var freed))
                {
                    matched++;

                    inUse -= freed.Bytes;

                    freed.Purpose.Free(freed.Bytes);

                    freed.Operator?.Free(freed.Bytes);

                    freed.Statement?.Free(freed.Bytes);
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

            totals.Allocate(node, memoryEvent.Bytes);

            if (!ownersOf.TryGetValue(node, out var owners))
            {
                owners = (OwnerOf(node, operatorsByFrame), OwnerOf(node, statementsByFrame));

                ownersOf[node] = owners;
            }

            var operatorTotals = owners.Operator is null ? null : TotalsOf(byOperator, owners.Operator);

            var statementTotals = owners.Statement is null ? null : TotalsOf(byOperator, owners.Statement);

            operatorTotals?.Allocate(node, memoryEvent.Bytes);

            statementTotals?.Allocate(node, memoryEvent.Bytes);

            if (!memoryEvent.Returned)
            {
                continue;
            }

            returned++;

            if (memoryEvent.Pointer != 0)
            {
                live[memoryEvent.Pointer] = (totals, operatorTotals, statementTotals, memoryEvent.Bytes);

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
                                           [.. purposes.OrderByDescending(p => p.Value.Allocated).Select(p => p.Value.ToPurpose(p.Key))]);
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

                    if (sizes.TryGetValue(frame.Address, out var bytes) && call < bytes.Length)
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

    private readonly record struct MemoryEvent(ulong Sequence,
                                               MemoryOperation Operation,
                                               CallStackNode? Node,
                                               ulong Bytes,
                                               ulong Pointer,
                                               bool Returned);

    private sealed class PurposeTotals
    {
        public ulong Allocated { get; private set; }

        private ulong InUse { get; set; }

        private ulong Peak { get; set; }

        private ulong Freed { get; set; }

        private long Count { get; set; }

        private Dictionary<(string Caller, string Allocator), (ulong Bytes, long Count)> Uses { get; } = [];

        public void Allocate(CallStackNode node, ulong bytes)
        {
            Allocated += bytes;

            Count++;

            InUse += bytes;

            Peak = Math.Max(Peak, InUse);

            var caller = CallerOf(node);

            var key = (caller is { IsRoot: false } ? caller.Symbol : NoOperator, EntryOf(node, caller).Symbol);

            Uses[key] = Uses.TryGetValue(key, out var use) ? (use.Bytes + bytes, use.Count + 1) : (bytes, 1);
        }

        public void Free(ulong bytes)
        {
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
                           .Select(u => new TimeTravelMemoryUse(u.Key.Caller, u.Key.Allocator, u.Value.Bytes, u.Value.Count))]);
    }
}
