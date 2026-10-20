using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceRowReturnMarks
{
    private const uint RowStatus = 0;

    private const uint EndStatus = 0x40EC6;

    private const uint FakeRowStatus = 0x4FFFF;

    private const string BatchMethod = "BpGetNextBatch";

    private const string FilterOperator = "Filter";

    private static readonly int TypeCount = Enum.GetValues<RowReturnType>().Length;

    private TraceRowReturnMarks(Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> operators,
                                HashSet<ExecutionOperatorEvent> batchOperators,
                                TraceOutputRows outputRows,
                                TraceTickSeries ticks)
    {
        Operators = operators;

        Ticks = ticks;

        BatchOperators = batchOperators;

        OutputRows = outputRows;

        Count = operators.Values.Sum(o => o.Values.Sum(t => t.Count));
    }

    public static TraceRowReturnMarks Empty { get; } = new(new(ReferenceEqualityComparer.Instance),
                                                           new(ReferenceEqualityComparer.Instance),
                                                           TraceOutputRows.Empty,
                                                           TraceTickSeries.Empty);

    public int Count { get; }

    public TraceOutputRows OutputRows { get; }

    public TraceTickSeries Ticks { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> Operators { get; }

    private HashSet<ExecutionOperatorEvent> BatchOperators { get; }

    public bool IsBatch(ExecutionOperatorEvent operatorEvent) => BatchOperators.Contains(operatorEvent);

    public bool HasMarks(ExecutionOperatorEvent operatorEvent, uint thread) => GetReturns(operatorEvent, thread) is not null;

    public ReadOnlySpan<double> GetMarks(ExecutionOperatorEvent operatorEvent, uint thread, TraceTimelineAxis axis, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.Of(axis).GetValues(type) : [];

    public int GetCount(ExecutionOperatorEvent operatorEvent, uint thread, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.Positions.GetValues(type).Length : 0;

    internal static TraceRowReturnMarks Build(TraceTimeline timeline,
                                              TraceCallStore calls,
                                              IReadOnlyList<TraceOutputRowSource>? sources = null)
    {
        var owners = timeline.GetOperatorsByInstance();

        if (owners.Count == 0)
        {
            return Empty;
        }

        var groups = GetCallGroups(calls, owners);

        SetEnclosing(calls, owners, groups);

        var groupsByInstance = groups.ToDictionary(g => (g.Instance, g.Thread));

        var marks = new Dictionary<(ExecutionOperatorEvent Operator, uint Thread), List<Mark>>(OperatorThreadComparer.Instance);

        var ticks = new List<TraceTick>();

        var passedUp = new List<TraceRowReturn>();

        var passedUpCalls = new Dictionary<(ulong Instance, uint Thread, int Index), TraceRowReturn>();

        var consumers = new List<(TraceRowReturn Return, (ulong Instance, uint Thread, int Index) Consumer)>();

        var batchOperators = new HashSet<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance);

        foreach (var group in groups)
        {
            var operatorEvent = owners[group.Instance];

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

                if (GetReturnType(group, index, parent, caller?.Name == FilterOperator, IsStored(operatorEvent, caller)) is not { } type
                    || timeline.FindCall(call.Function, group.Instance, call.Call) is not { } location)
                {
                    continue;
                }

                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(marks, (operatorEvent, group.Thread), out _);

                var mark = new Mark(location.EndOf(TraceTimelineAxis.Position),
                                    location.EndOf(TraceTimelineAxis.Instructions),
                                    location.StartOf(TraceTimelineAxis.Position),
                                    location.StartOf(TraceTimelineAxis.Instructions),
                                    type,
                                    location.Node,
                                    location.Call);

                (list ??= []).Add(mark);

                ticks.Add(new TraceTick((byte)type,
                                        group.Thread,
                                        location.Node,
                                        location.Call,
                                        location.Span(TraceTimelineAxis.Position),
                                        location.Span(TraceTimelineAxis.Instructions))
                {
                    Operator = operatorEvent,
                    Caller = ReferenceEquals(caller, operatorEvent) ? null : caller,
                    Function = call.Function,
                    Instance = group.Instance,
                    Index = call.Call
                });

                if (type == RowReturnType.PassedUp && !group.IsBatch)
                {
                    var rowReturn = new TraceRowReturn(operatorEvent,
                                                       group.Thread,
                                                       mark.Node,
                                                       mark.Call,
                                                       type,
                                                       mark.Position,
                                                       mark.Instructions)
                    {
                        StartPosition = mark.StartPosition,
                        StartInstructions = mark.StartInstructions,
                        FromPosition = call.FromPosition,
                        ToPosition = call.ToPosition
                    };

                    passedUp.Add(rowReturn);

                    passedUpCalls[(group.Instance, group.Thread, index)] = rowReturn;

                    if (parent is not null && GetConsumerCall(parent, call) is { } consumer)
                    {
                        consumers.Add((rowReturn, (parent.Instance, parent.Thread, consumer)));
                    }
                }
            }
        }

        foreach (var (rowReturn, consumer) in consumers)
        {
            rowReturn.Consumer = passedUpCalls.GetValueOrDefault(consumer);
        }

        var operators = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>>(ReferenceEqualityComparer.Instance);

        foreach (var ((operatorEvent, thread), list) in marks)
        {
            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(operators, operatorEvent, out _);

            (threads ??= [])[thread] = OperatorReturns.Build(list);
        }

        var outputRows = TraceOutputRows.Build(groups.Select(g => owners[g.Instance]), passedUp, sources ?? []);

        return new TraceRowReturnMarks(operators, batchOperators, outputRows, TraceTickSeries.Create(ticks));
    }

    private static List<CallGroup> GetCallGroups(TraceCallStore calls, Dictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var grouped = new Dictionary<(ulong Instance, uint Thread), List<GetRowCall>>();

        var batches = new HashSet<(ulong Instance, uint Thread)>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance)
                || calls.GetCallList(address, instance) is not { Count: > 0 } list
                || calls.GetNode(list.GetNodeIndex(0))?.Frame?.Resolved?.MethodName is not { } method
                || !IsIteratorCall(method))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var key = (instance, list.GetThread(index));

                if (method == BatchMethod)
                {
                    batches.Add(key);
                }

                ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, key, out _);

                (group ??= []).Add(new GetRowCall(address,
                                                  index,
                                                  list.GetFromPosition(index),
                                                  list.GetToPosition(index),
                                                  list.GetReturnValue(index) is { } value ? (uint)value : null));
            }
        }

        return [.. grouped.Select(g => new CallGroup(g.Key.Instance, g.Key.Thread, GetOutermost(g.Value), batches.Contains(g.Key)))];
    }

    private static GetRowCall[] GetOutermost(List<GetRowCall> calls)
    {
        calls.Sort((left, right) => left.FromPosition.CompareTo(right.FromPosition));

        var outermost = new List<GetRowCall>(calls.Count);

        foreach (var call in calls)
        {
            if (outermost.Count > 0 && call.FromPosition < outermost[^1].ToPosition)
            {
                continue;
            }

            outermost.Add(call);
        }

        return [.. outermost];
    }

    private static void SetEnclosing(TraceCallStore calls, Dictionary<ulong, ExecutionOperatorEvent> owners, List<CallGroup> groups)
    {
        var threads = new Dictionary<uint, List<SweepCall>>();

        foreach (var group in groups)
        {
            for (var index = 0; index < group.Calls.Length; index++)
            {
                var call = group.Calls[index];

                Add(group.Thread, new SweepCall(call.FromPosition, call.ToPosition, group.Instance, group, index));
            }
        }

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance)
                || calls.GetCallList(address, instance) is not { Count: > 0 } list
                || IsIteratorCall(calls.GetNode(list.GetNodeIndex(0))?.Frame?.Resolved?.MethodName ?? string.Empty))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                Add(list.GetThread(index), new SweepCall(list.GetFromPosition(index), list.GetToPosition(index), instance, null, -1));
            }
        }

        var open = new Stack<SweepCall>();

        foreach (var thread in threads.Values)
        {
            thread.Sort((left, right) => left.From != right.From ? left.From.CompareTo(right.From) : right.To.CompareTo(left.To));

            open.Clear();

            foreach (var call in thread)
            {
                while (open.TryPeek(out var outer) && outer.To <= call.From)
                {
                    open.Pop();
                }

                if (call.Group is { } group)
                {
                    group.Enclosing[call.Index] = open.TryPeek(out var enclosing) ? enclosing.Instance : 0;
                }

                open.Push(call);
            }
        }

        void Add(uint thread, SweepCall call)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (list ??= []).Add(call);
        }
    }

    private static int? GetConsumerCall(CallGroup parent, GetRowCall call)
    {
        var index = SortedSearch.FirstAfter(parent.Calls, call.FromPosition, c => c.FromPosition) - 1;

        return index >= 0 && parent.Calls[index].ToPosition >= call.ToPosition ? index : null;
    }

    private static RowReturnType? GetReturnType(CallGroup group, int index, CallGroup? parent, bool isFiltered, bool isStored)
        => group.Calls[index].Status switch
        {
            null => null,
            RowStatus when isStored => RowReturnType.Stored,
            RowStatus when parent is null || IsPassedUp(group, index, parent) => RowReturnType.PassedUp,
            RowStatus => isFiltered ? RowReturnType.Rejected : RowReturnType.NotPassedUp,
            EndStatus => RowReturnType.End,
            FakeRowStatus => RowReturnType.FakeRow,
            _ => RowReturnType.Other
        };

    private static bool IsStored(ExecutionOperatorEvent operatorEvent, ExecutionOperatorEvent? caller)
        => operatorEvent.IsBlockingInput && caller is { Category: not OperatorCategory.Adapter };

    private static bool IsPassedUp(CallGroup group, int index, CallGroup parent)
    {
        var next = index + 1 < group.Calls.Length ? group.Calls[index + 1].FromPosition : TracePosition.Unknown;

        var returned = SortedSearch.FirstAtOrAfter(parent.RowEnds, group.Calls[index].ToPosition, p => p);

        return returned < parent.RowEnds.Length && parent.RowEnds[returned] < next;
    }

    private static bool IsIteratorCall(string method) => method is "GetRow" or "GetRowOrReQualifyHelper" or BatchMethod;

    private OperatorReturns? GetReturns(ExecutionOperatorEvent operatorEvent, uint thread)
        => Operators.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    private readonly record struct GetRowCall(ulong Function, int Call, TracePosition FromPosition, TracePosition ToPosition, uint? Status);

    private readonly record struct Mark(double Position,
                                        double Instructions,
                                        double StartPosition,
                                        double StartInstructions,
                                        RowReturnType Type,
                                        CallStackNode Node,
                                        int Call);

    private sealed class CallGroup(ulong instance, uint thread, GetRowCall[] calls, bool isBatch)
    {
        public ulong Instance { get; } = instance;

        public uint Thread { get; } = thread;

        public GetRowCall[] Calls { get; } = calls;

        public bool IsBatch { get; } = isBatch;

        public TracePosition[] RowEnds { get; } = [.. calls.Where(c => c.Status == RowStatus).Select(c => c.ToPosition)];

        public ulong[] Enclosing { get; } = new ulong[calls.Length];
    }

    private readonly record struct SweepCall(TracePosition From, TracePosition To, ulong Instance, CallGroup? Group, int Index);

    private sealed record AxisReturns(double[] Values, double[][] TypeValues)
    {
        public ReadOnlySpan<double> GetValues(RowReturnType type) => TypeValues[(int)type];

        public static AxisReturns Build(List<Mark> marks, Func<Mark, double> getValue)
        {
            Mark[] ordered = [.. marks.OrderBy(getValue)];

            var typeValues = new double[TypeCount][];

            for (var type = 0; type < TypeCount; type++)
            {
                typeValues[type] = [.. ordered.Where(m => (int)m.Type == type).Select(getValue)];
            }

            return new AxisReturns([.. ordered.Select(getValue)], typeValues);
        }
    }

    private sealed class OperatorThreadComparer : IEqualityComparer<(ExecutionOperatorEvent Operator, uint Thread)>
    {
        public static OperatorThreadComparer Instance { get; } = new();

        public bool Equals((ExecutionOperatorEvent Operator, uint Thread) left, (ExecutionOperatorEvent Operator, uint Thread) right)
            => ReferenceEquals(left.Operator, right.Operator) && left.Thread == right.Thread;

        public int GetHashCode((ExecutionOperatorEvent Operator, uint Thread) value)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(value.Operator), value.Thread);
    }

    private sealed record OperatorReturns(AxisReturns Positions, AxisReturns Instructions)
    {
        public int Count => Positions.Values.Length;

        public AxisReturns Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

        public static OperatorReturns Build(List<Mark> marks)
            => new(AxisReturns.Build(marks, m => m.Position), AxisReturns.Build(marks, m => m.Instructions));
    }
}
