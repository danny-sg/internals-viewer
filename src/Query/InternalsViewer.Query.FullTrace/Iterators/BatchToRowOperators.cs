using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class BatchToRowOperators
{
    private const string HelperClass = "CQScanBatchHelper";

    private const string OperatorName = "Batch To Row";

    public static List<ExecutionOperatorEvent> Create(TraceTimeline timeline,
                                                      BatchStore batches,
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

        var helpers = timeline.MappedNodes
                              .Select(n => n?.Frame)
                              .Where(f => f is { Instance: not 0, Resolved.ClassName: HelperClass })
                              .Select(f => f!.Instance)
                              .Distinct();

        var adapters = new List<ExecutionOperatorEvent>();

        foreach (var helper in helpers)
        {
            if (!owners.ContainsKey(helper)
                && batches.GetProducer(helper) is { } producer
                && owners.TryGetValue(producer, out var source))
            {
                adapters.Add(new ExecutionOperatorEvent
                {
                    Name = OperatorName,
                    OperatorDescription = OperatorName,
                    LogicalOperator = OperatorName,
                    Category = OperatorCategory.Adapter,
                    NodeLevel = source.NodeLevel,
                    Instances = [helper]
                });
            }
        }

        return adapters;
    }
}
