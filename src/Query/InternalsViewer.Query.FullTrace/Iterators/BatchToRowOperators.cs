using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class BatchToRowOperators
{
    private const string OperatorName = "Batch To Row";

    public static List<ExecutionOperatorEvent> Create(IReadOnlyList<IteratorPlanNode> planNodes,
                                                      IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var operatorEvent in operators)
        {
            foreach (var instance in operatorEvent.Instances)
            {
                owners.TryAdd(instance, operatorEvent);
            }
        }

        var adapters = new List<ExecutionOperatorEvent>();

        foreach (var helpers in IteratorPlanLinker.GetAdapters(planNodes).GroupBy(a => a.Value))
        {
            if (owners.TryGetValue(helpers.Key, out var source))
            {
                adapters.Add(new ExecutionOperatorEvent
                {
                    Name = OperatorName,
                    OperatorDescription = OperatorName,
                    LogicalOperator = OperatorName,
                    Category = OperatorCategory.Adapter,
                    NodeLevel = source.NodeLevel,
                    IsBlockingInput = source.IsBlockingInput,
                    Instances = [.. helpers.Select(h => h.Key)]
                });
            }
        }

        return adapters;
    }
}
