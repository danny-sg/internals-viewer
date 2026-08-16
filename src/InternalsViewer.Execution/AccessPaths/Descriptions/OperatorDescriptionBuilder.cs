using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Descriptions.Aggregation;
using InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;
using InternalsViewer.Execution.AccessPaths.Descriptions.Joins;
using InternalsViewer.Execution.AccessPaths.Descriptions.Row;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions;

/// <summary>
/// Hands an operator to the describer that knows it, the same dispatch on definition the iterator factory makes
/// </summary>
/// <remarks>
/// An access path takes its phases from the strategy, which is the only description that depends on what the definition resolved to
/// against a database. Every other operator describes itself from its definition alone.
/// </remarks>
public static class OperatorDescriptionBuilder
{
    public static OperatorDescription Build(IteratorDefinition definition, AccessStrategy? strategy)
        => definition switch
        {
            NestedLoopsDefinition loops => NestedLoopsDescriber.Describe(loops),
            MergeJoinDefinition merge => MergeJoinDescriber.Describe(merge),
            HashMatchDefinition hash => HashMatchDescriber.Describe(hash),
            TopDefinition top => TopDescriber.Describe(top),
            SortDefinition sort => SortDescriber.Describe(sort),
            StreamAggregateDefinition aggregate => StreamAggregateDescriber.Describe(aggregate),
            HashAggregateDefinition hashAggregate => HashAggregateDescriber.Describe(hashAggregate),
            ComputeScalarDefinition compute => ComputeScalarDescriber.Describe(compute),
            ConcatenationDefinition concatenation => ConcatenationDescriber.Describe(concatenation),
            SelectDefinition => SelectDescriber.Describe(),
            SeekDefinition => CorrelatedSeekDescriber.Describe(strategy),
            RangeDefinition range => IndexDescriber.Describe(range, strategy),
            AllocationScanDefinition => AllocationScanDescriber.Describe(strategy),
            HeapFetchDefinition => HeapFetchDescriber.Describe(strategy),
            _ => new OperatorDescription { IsStreaming = true, Phases = strategy?.Phases ?? [] }
        };
}
