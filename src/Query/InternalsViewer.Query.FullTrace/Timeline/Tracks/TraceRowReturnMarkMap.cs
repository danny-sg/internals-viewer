using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed class TraceRowReturnMarkMap
{
    private const uint RowStatus = 0;

    private const uint EndStatus = 0x40EC6;

    private const uint FakeRowStatus = 0x4FFFF;

    private const string FilterOperator = "Filter";

    private TraceRowReturnMarkMap(HashSet<ExecutionOperatorEvent> batchOperators, TraceTickSeries ticks)
    {
        Ticks = ticks;

        BatchOperators = batchOperators;
    }

    public static TraceRowReturnMarkMap Empty { get; } = new(new(ReferenceEqualityComparer.Instance), TraceTickSeries.Empty);

    public int Count => Ticks.Count;

    public TraceTickSeries Ticks { get; }

    private HashSet<ExecutionOperatorEvent> BatchOperators { get; }

    public bool IsBatch(ExecutionOperatorEvent operatorEvent) => BatchOperators.Contains(operatorEvent);

    public IEnumerable<ExecutionOperatorEvent> GetRequested(TracePosition position) => Ticks.GetRunning(position);

    public IEnumerable<ExecutionOperatorEvent> GetReturned(TracePosition after, TracePosition upTo)
        => Ticks.GetEnding(after, upTo, static v => v != (byte)RowReturnType.End);

    internal static TraceRowReturnMarkMap Build(TraceTimeline timeline,
                                              TraceCallStore calls,
                                              TraceOperatorLifetimeList lifetimes,
                                              EnclosingOperatorCallIndex? enclosingCalls = null)
    {
        var owners = lifetimes.OperatorsByInstance;

        if (owners.Count == 0)
        {
            return Empty;
        }

        var groups = GetCallGroups(calls, owners);

        SetEnclosing(enclosingCalls ?? EnclosingOperatorCallIndex.Build(calls, owners), groups);

        var groupsByInstance = groups.ToDictionary(g => (g.InstanceAddress, g.Thread));

        var ticks = new List<TraceTickSource>();

        var batchOperators = new HashSet<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance);

        foreach (var group in groups)
        {
            var operatorEvent = owners[group.InstanceAddress];

            if (group.IsBatch)
            {
                batchOperators.Add(operatorEvent);
            }

            for (var index = 0; index < group.Calls.Length; index++)
            {
                var call = group.Calls[index];

                var enclosing = group.Enclosing[index];

                var caller = enclosing == 0 ? null : owners[enclosing];

                var parent = enclosing == 0 ? null : groupsByInstance.GetValueOrDefault((enclosing, group.Thread));

                if (timeline.FindCall(call.FunctionAddress, group.InstanceAddress, call.CallIndex) is not { } location
                    || GetReturnType(group, index, parent, caller?.Name == FilterOperator, operatorEvent.IsBlockingInput) is not { } type)
                {
                    continue;
                }

                ticks.Add(TraceTickSource.FromCall(location, (byte)type) with
                {
                    Operator = operatorEvent,
                    Caller = ReferenceEquals(caller, operatorEvent) ? null : caller
                });
            }
        }

        return new TraceRowReturnMarkMap(batchOperators, TraceTickSeries.Create(timeline, ticks));
    }

    private static List<CallGroup> GetCallGroups(TraceCallStore calls, IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var grouped = new Dictionary<(ulong InstanceAddress, uint Thread), List<GetRowCall>>();

        var batches = new HashSet<(ulong InstanceAddress, uint Thread)>();

        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var list in calls.GetCallLists())
        {
            if (!owners.ContainsKey(list.InstanceAddress)
                || calls.GetFirstNode(list)?.Frame?.Function?.MethodName is not { } method
                || !IteratorSymbolLookup.IsRowReturnMethod(method))
            {
                continue;
            }

            foreach (var (columns, first) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.Calls; callIndex++)
                {
                    var key = (list.InstanceAddress, columns.GetThread(callIndex));

                    if (method == IteratorSymbolLookup.GetNextBatchMethod)
                    {
                        batches.Add(key);
                    }

                    ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, key, out _);

                    (group ??= []).Add(new GetRowCall(list.FunctionAddress,
                                                      first + callIndex,
                                                      columns.GetFromPosition(callIndex),
                                                      columns.GetToPosition(callIndex),
                                                      columns.IsReturned(callIndex) ? (uint)columns.GetReturnValue(callIndex) : null));
                }
            }
        }

        return [.. grouped.Select(g => new CallGroup(g.Key.InstanceAddress, g.Key.Thread, GetOutermost(g.Value), batches.Contains(g.Key)))];
    }

    private static GetRowCall[] GetOutermost(List<GetRowCall> calls) => [.. Outermost.Of(calls, c => c.FromPosition, c => c.ToPosition)];

    private static void SetEnclosing(EnclosingOperatorCallIndex calls, List<CallGroup> groups)
    {
        var points = new List<SweepSpan<TracePosition>>();

        foreach (var group in groups)
        {
            foreach (var call in group.Calls)
            {
                points.Add(new SweepSpan<TracePosition>(group.Thread, call.FromPosition, call.ToPosition, group.InstanceAddress));
            }
        }

        var enclosing = calls.GetEnclosing(points, includeRowReturns: false);

        var point = 0;

        foreach (var group in groups)
        {
            for (var index = 0; index < group.Calls.Length; index++)
            {
                group.Enclosing[index] = enclosing[point++];
            }
        }
    }

    private static RowReturnType? GetReturnType(CallGroup group, int index, CallGroup? parent, bool isFiltered, bool isBlockingInput)
        => group.Calls[index].Status switch
        {
            null => null,
            RowStatus when parent is null || isBlockingInput || IsPassedUp(group, index, parent) => RowReturnType.Returned,
            RowStatus => isFiltered ? RowReturnType.Rejected : RowReturnType.Skipped,
            EndStatus => RowReturnType.End,
            FakeRowStatus => RowReturnType.FakeRow,
            _ => RowReturnType.Other
        };

    private static bool IsPassedUp(CallGroup group, int index, CallGroup parent)
    {
        var next = index + 1 < group.Calls.Length ? group.Calls[index + 1].FromPosition : TracePosition.Unknown;

        var returned = SortedSearch.FirstAtOrAfter<TracePosition>(parent.RowEnds, group.Calls[index].ToPosition);

        return returned < parent.RowEnds.Length && parent.RowEnds[returned] < next;
    }

    private readonly record struct GetRowCall(ulong FunctionAddress,
                                              int CallIndex,
                                              TracePosition FromPosition,
                                              TracePosition ToPosition,
                                              uint? Status);

    private sealed class CallGroup(ulong instanceAddress, uint thread, GetRowCall[] calls, bool isBatch)
    {
        public ulong InstanceAddress { get; } = instanceAddress;

        public uint Thread { get; } = thread;

        public GetRowCall[] Calls { get; } = calls;

        public bool IsBatch { get; } = isBatch;

        public TracePosition[] RowEnds { get; } = [.. calls.Where(c => c.Status == RowStatus).Select(c => c.ToPosition)];

        public ulong[] Enclosing { get; } = new ulong[calls.Length];
    }
}
