using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceRowReturnMarks
{
    private const uint RowStatus = 0;

    private const uint EndStatus = 0x40EC6;

    private const uint FakeRowStatus = 0x4FFFF;

    private static readonly int TypeCount = Enum.GetValues<RowReturnType>().Length;

    private TraceRowReturnMarks(Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> operators)
    {
        Operators = operators;

        Count = operators.Values.Sum(o => o.Values.Sum(t => t.Count));
    }

    public static TraceRowReturnMarks Empty { get; } = new(new(ReferenceEqualityComparer.Instance));

    public int Count { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>> Operators { get; }

    public bool HasMarks(ExecutionOperatorEvent operatorEvent, uint thread) => GetReturns(operatorEvent, thread) is not null;

    public ReadOnlySpan<double> GetMarks(ExecutionOperatorEvent operatorEvent, uint thread, TraceTimelineAxis axis, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.Of(axis).GetValues(type) : [];

    public int GetCount(ExecutionOperatorEvent operatorEvent, uint thread, RowReturnType type)
        => GetReturns(operatorEvent, thread) is { } returns ? returns.Positions.GetValues(type).Length : 0;

    public (CallStackNode Node, int Call, RowReturnType Type)? GetNearestMark(ExecutionOperatorEvent operatorEvent,
                                                                             uint thread,
                                                                             TraceTimelineAxis axis,
                                                                             double value)
    {
        if (GetReturns(operatorEvent, thread) is not { } returns)
        {
            return null;
        }

        var marks = returns.Of(axis);

        var index = SortedSearch.FirstAtOrAfter(marks.Values, value);

        if (index > 0 && (index == marks.Values.Length || value - marks.Values[index - 1] <= marks.Values[index] - value))
        {
            index--;
        }

        return index < marks.Values.Length ? (marks.Nodes[index], marks.Calls[index], marks.Types[index]) : null;
    }

    internal static TraceRowReturnMarks Build(TraceTimeline timeline, TraceCallStore calls)
    {
        var owners = timeline.GetOperatorsByInstance();

        if (owners.Count == 0)
        {
            return Empty;
        }

        var groups = GetCallGroups(calls, owners);

        var marks = new Dictionary<(ExecutionOperatorEvent Operator, uint Thread), List<Mark>>(OperatorThreadComparer.Instance);

        foreach (var group in groups)
        {
            var parent = GetParent(group, groups);

            for (var index = 0; index < group.Calls.Length; index++)
            {
                var call = group.Calls[index];

                if (GetReturnType(group, index, parent) is not { } type
                    || timeline.FindCall(call.Function, group.Instance, call.Call) is not { } location)
                {
                    continue;
                }

                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(marks, (owners[group.Instance], group.Thread), out _);

                (list ??= []).Add(new Mark(location.EndOf(TraceTimelineAxis.Position),
                                           location.EndOf(TraceTimelineAxis.Instructions),
                                           type,
                                           location.Node,
                                           location.Call));
            }
        }

        var operators = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, OperatorReturns>>(ReferenceEqualityComparer.Instance);

        foreach (var ((operatorEvent, thread), list) in marks)
        {
            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(operators, operatorEvent, out _);

            (threads ??= [])[thread] = OperatorReturns.Build(list);
        }

        return new TraceRowReturnMarks(operators);
    }

    private static List<CallGroup> GetCallGroups(TraceCallStore calls, Dictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var grouped = new Dictionary<(ulong Instance, uint Thread), List<GetRowCall>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance)
                || calls.GetCallList(address, instance) is not { Count: > 0 } list
                || !IsGetRow(calls.GetNode(list.GetNodeIndex(0))))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, (instance, list.GetThread(index)), out _);

                (group ??= []).Add(new GetRowCall(address,
                                                  index,
                                                  list.GetFromPosition(index),
                                                  list.GetToPosition(index),
                                                  list.GetReturnValue(index) is { } value ? (uint)value : null));
            }
        }

        return [.. grouped.Select(g => new CallGroup(g.Key.Instance, g.Key.Thread, GetOutermost(g.Value)))];
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

    private static CallGroup? GetParent(CallGroup group, List<CallGroup> groups)
    {
        var first = group.Calls[0];

        CallGroup? parent = null;

        var parentStart = TracePosition.Unknown;

        foreach (var other in groups)
        {
            if (other.Thread != group.Thread || other.Instance == group.Instance)
            {
                continue;
            }

            var index = SortedSearch.FirstAfter(other.Calls, first.FromPosition, c => c.FromPosition) - 1;

            if (index < 0 || other.Calls[index].ToPosition < first.ToPosition)
            {
                continue;
            }

            if (parent is null || other.Calls[index].FromPosition > parentStart)
            {
                parent = other;

                parentStart = other.Calls[index].FromPosition;
            }
        }

        return parent;
    }

    private static RowReturnType? GetReturnType(CallGroup group, int index, CallGroup? parent)
        => group.Calls[index].Status switch
        {
            null => null,
            RowStatus => parent is null || IsPassedUp(group, index, parent) ? RowReturnType.PassedUp : RowReturnType.NotPassedUp,
            EndStatus => RowReturnType.End,
            FakeRowStatus => RowReturnType.FakeRow,
            _ => RowReturnType.Other
        };

    private static bool IsPassedUp(CallGroup group, int index, CallGroup parent)
    {
        var next = index + 1 < group.Calls.Length ? group.Calls[index + 1].FromPosition : TracePosition.Unknown;

        var returned = SortedSearch.FirstAtOrAfter(parent.RowEnds, group.Calls[index].ToPosition, p => p);

        return returned < parent.RowEnds.Length && parent.RowEnds[returned] < next;
    }

    private static bool IsGetRow(CallStackNode? node) => node?.Frame?.Resolved?.MethodName is "GetRow" or "GetRowOrReQualifyHelper";

    private OperatorReturns? GetReturns(ExecutionOperatorEvent operatorEvent, uint thread)
        => Operators.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    private readonly record struct GetRowCall(ulong Function, int Call, TracePosition FromPosition, TracePosition ToPosition, uint? Status);

    private readonly record struct Mark(double Position, double Instructions, RowReturnType Type, CallStackNode Node, int Call);

    private sealed class CallGroup(ulong instance, uint thread, GetRowCall[] calls)
    {
        public ulong Instance { get; } = instance;

        public uint Thread { get; } = thread;

        public GetRowCall[] Calls { get; } = calls;

        public TracePosition[] RowEnds { get; } = [.. calls.Where(c => c.Status == RowStatus).Select(c => c.ToPosition)];
    }

    private sealed record AxisReturns(double[] Values, RowReturnType[] Types, CallStackNode[] Nodes, int[] Calls, double[][] TypeValues)
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

            return new AxisReturns([.. ordered.Select(getValue)],
                                   [.. ordered.Select(m => m.Type)],
                                   [.. ordered.Select(m => m.Node)],
                                   [.. ordered.Select(m => m.Call)],
                                   typeValues);
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
