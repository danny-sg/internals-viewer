using InternalsViewer.Replay.Events.EventTypes;

namespace InternalsViewer.Replay.Plans;

internal sealed class NodeTiming(List<EngineEvent> events, ExecutionPlan plan)
{
    private List<EngineEvent> Events { get; } = events;

    private ExecutionPlan Plan { get; } = plan;

    private readonly Dictionary<int, long> _startTimes = new();

    private readonly Dictionary<int, long> _endTimes = new();

    public void Build()
    {
        foreach (var rootNode in Plan.Root)
        {
            GetStartTime(rootNode);
            GetEndTime(rootNode);
        }
    }

    public long GetStartTime(PlanNode node)
    {
        if (_startTimes.TryGetValue(node.NodeId, out var cached))
        {
            return cached;
        }

        var start = OperatorClassifier.InferStartTime(node, Plan.PlanHandle, Events, GetStartTime);

        _startTimes[node.NodeId] = start;

        return start;
    }

    public long GetEndTime(PlanNode node)
    {
        if (_endTimes.TryGetValue(node.NodeId, out var cached))
        {
            return cached;
        }

        var end = OperatorClassifier.InferEndTime(node, Plan.PlanHandle, Events, GetEndTime);

        _endTimes[node.NodeId] = end;

        return end;
    }

}
