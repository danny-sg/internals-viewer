using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Execution.Common.Iterators.Common;

namespace InternalsViewer.Execution.BatchMode.Interfaces;

/// <summary>
/// Target of a Batch Mode internal aggregation
/// </summary>
public interface IAggregatePushdownTarget
{
    bool IsAggregatePushdown { get; }

    long LocallyAggregatedRows { get; }

    void SetPushdownSink(HashAggregateBuilder builder, EvaluationContext context);
}
