using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.Iterators.Common;

namespace InternalsViewer.Execution.Interfaces.BatchMode;

/// <summary>
/// Target of a Batch Mode internal aggregation
/// </summary>
public interface IAggregatePushdownTarget
{
    bool IsAggregatePushdown { get; }

    long LocallyAggregatedRows { get; }

    void SetPushdownSink(HashAggregateBuilder builder, EvaluationContext context);
}
