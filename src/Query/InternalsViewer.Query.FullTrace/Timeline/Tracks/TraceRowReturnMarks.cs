using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed class TraceRowReturnMarks
{
    private const uint RowStatus = 0;

    private const uint EndStatus = 0x40EC6;

    private const uint FakeRowStatus = 0x4FFFF;

    private const string FilterOperator = "Filter";

    private const string HashClass = "CQScanHash";

    private const string ConsumeBuildMethod = "ConsumeBuild";

    private static readonly int TypeCount = Enum.GetValues<RowReturnType>().Length;

    private TraceRowReturnMarks(Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> operators,
                                HashSet<ExecutionOperatorEvent> batchOperators,
                                TraceTickSeries ticks)
    {
        Operators = operators;

        Ticks = ticks;

        BatchOperators = batchOperators;

        Count = operators.Values.Sum(o => o.Values.Sum(t => t.Count));
    }

    public static TraceRowReturnMarks Empty { get; } = new(new(ReferenceEqualityComparer.Instance),
                                                           new(ReferenceEqualityComparer.Instance),
                                                           TraceTickSeries.Empty);

    public int Count { get; }

    public TraceTickSeries Ticks { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> Operators { get; }

    private HashSet<ExecutionOperatorEvent> BatchOperators { get; }

    public bool IsBatch(ExecutionOperatorEvent operatorEvent) => BatchOperators.Contains(operatorEvent);

    public ReadOnlySpan<TracePoint> GetMarks(ExecutionOperatorEvent operatorEvent, uint thread, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.GetPoints(type) : [];

    public int GetCount(ExecutionOperatorEvent operatorEvent, uint thread, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.GetPoints(type).Length : 0;

    internal static TraceRowReturnMarks Build(TraceTimeline timeline,
                                              TraceCallStore calls,
                                              TraceOperatorLifetimes lifetimes,
                                              EnclosingOperatorCalls? enclosingCalls = null)
    {
        var owners = lifetimes.OperatorsByInstance;

        if (owners.Count == 0)
        {
            return Empty;
        }

        var groups = GetCallGroups(calls, owners);

        SetEnclosing(enclosingCalls ?? EnclosingOperatorCalls.Build(calls, owners), groups);

        var groupsByInstance = groups.ToDictionary(g => (g.InstanceAddress, g.Thread));

        var marks = new Dictionary<(ExecutionOperatorEvent Operator, uint Thread), List<Mark>>(OperatorThreadComparer.Instance);

        var ticks = new List<TraceTick>();

        var batchOperators = new HashSet<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance);

        var buildInputs = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

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
                    || GetReturnType(group,
                                     index,
                                     parent,
                                     caller?.Name == FilterOperator,
                                     IsBuildInput(location.Node, enclosing, buildInputs)) is not { } type)
                {
                    continue;
                }

                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(marks, (operatorEvent, group.Thread), out _);

                (list ??= []).Add(new Mark(location.To, type));

                ticks.Add(new TraceTick((byte)type, group.Thread, location.Node, location.Row, location.Index)
                {
                    Operator = operatorEvent,
                    Caller = ReferenceEquals(caller, operatorEvent) ? null : caller
                });
            }
        }

        var operators = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>>(ReferenceEqualityComparer.Instance);

        foreach (var ((operatorEvent, thread), list) in marks)
        {
            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(operators, operatorEvent, out _);

            (threads ??= [])[thread] = OperatorReturns.Build(list);
        }

        return new TraceRowReturnMarks(operators, batchOperators, TraceTickSeries.Create(ticks, timeline.Scale));
    }

    private static List<CallGroup> GetCallGroups(TraceCallStore calls, IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var grouped = new Dictionary<(ulong InstanceAddress, uint Thread), List<GetRowCall>>();

        var batches = new HashSet<(ulong InstanceAddress, uint Thread)>();

        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var (functionAddress, instanceAddress) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instanceAddress)
                || calls.GetCallList(functionAddress, instanceAddress) is not { Count: > 0 } list
                || calls.GetNode(list.GetCallNodeIndex(0))?.Frame?.Resolved?.MethodName is not { } method
                || !IteratorSymbols.IsRowReturnMethod(method))
            {
                continue;
            }

            foreach (var (columns, first) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.Calls; callIndex++)
                {
                    var key = (instanceAddress, columns.GetThread(callIndex));

                    if (method == IteratorSymbols.GetNextBatchMethod)
                    {
                        batches.Add(key);
                    }

                    ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, key, out _);

                    (group ??= []).Add(new GetRowCall(functionAddress,
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

    private static void SetEnclosing(EnclosingOperatorCalls calls, List<CallGroup> groups)
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

    private static RowReturnType? GetReturnType(CallGroup group, int index, CallGroup? parent, bool isFiltered, bool isBuildInput)
        => group.Calls[index].Status switch
        {
            null => null,
            RowStatus when parent is null || isBuildInput || IsPassedUp(group, index, parent) => RowReturnType.Returned,
            RowStatus => isFiltered ? RowReturnType.Rejected : RowReturnType.Skipped,
            EndStatus => RowReturnType.End,
            FakeRowStatus => RowReturnType.FakeRow,
            _ => RowReturnType.Other
        };

    private static bool IsBuildInput(CallStackNode node, ulong caller, Dictionary<CallStackNode, bool> known)
    {
        if (caller == 0)
        {
            return false;
        }

        if (known.TryGetValue(node, out var isBuildInput))
        {
            return isBuildInput;
        }

        for (var ancestor = node.Parent; ancestor?.Frame is { } frame && frame.InstanceAddress != caller; ancestor = ancestor.Parent)
        {
            if (frame.Resolved is { ClassName: HashClass, MethodName: ConsumeBuildMethod })
            {
                isBuildInput = true;

                break;
            }
        }

        known[node] = isBuildInput;

        return isBuildInput;
    }

    private static bool IsPassedUp(CallGroup group, int index, CallGroup parent)
    {
        var next = index + 1 < group.Calls.Length ? group.Calls[index + 1].FromPosition : TracePosition.Unknown;

        var returned = SortedSearch.FirstAtOrAfter<TracePosition>(parent.RowEnds, group.Calls[index].ToPosition);

        return returned < parent.RowEnds.Length && parent.RowEnds[returned] < next;
    }

    private OperatorReturns? GetReturns(ExecutionOperatorEvent operatorEvent, uint thread)
        => Operators.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    private readonly record struct GetRowCall(ulong FunctionAddress,
                                              int CallIndex,
                                              TracePosition FromPosition,
                                              TracePosition ToPosition,
                                              uint? Status);

    private readonly record struct Mark(TracePoint Point, RowReturnType Type);

    private sealed class CallGroup(ulong instanceAddress, uint thread, GetRowCall[] calls, bool isBatch)
    {
        public ulong InstanceAddress { get; } = instanceAddress;

        public uint Thread { get; } = thread;

        public GetRowCall[] Calls { get; } = calls;

        public bool IsBatch { get; } = isBatch;

        public TracePosition[] RowEnds { get; } = [.. calls.Where(c => c.Status == RowStatus).Select(c => c.ToPosition)];

        public ulong[] Enclosing { get; } = new ulong[calls.Length];
    }

    private sealed class OperatorThreadComparer : IEqualityComparer<(ExecutionOperatorEvent Operator, uint Thread)>
    {
        public static OperatorThreadComparer Instance { get; } = new();

        public bool Equals((ExecutionOperatorEvent Operator, uint Thread) left, (ExecutionOperatorEvent Operator, uint Thread) right)
            => ReferenceEquals(left.Operator, right.Operator) && left.Thread == right.Thread;

        public int GetHashCode((ExecutionOperatorEvent Operator, uint Thread) value)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Operator), value.Thread);
    }

    private sealed class OperatorReturns(TracePoint[][] typePoints)
    {
        public int Count { get; } = typePoints.Sum(p => p.Length);

        private TracePoint[][] TypePoints { get; } = typePoints;

        public ReadOnlySpan<TracePoint> GetPoints(RowReturnType type) => TypePoints[(int)type];

        public static OperatorReturns Build(List<Mark> marks)
        {
            var typePoints = new TracePoint[TypeCount][];

            for (var type = 0; type < TypeCount; type++)
            {
                typePoints[type] = [.. marks.Where(m => (int)m.Type == type).OrderBy(m => m.Point.Position).Select(m => m.Point)];
            }

            return new OperatorReturns(typePoints);
        }
    }
}
