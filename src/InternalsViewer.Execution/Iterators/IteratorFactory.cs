using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.RowMode.Aggregation;
using InternalsViewer.Execution.Iterators.RowMode.DataAccess;
using InternalsViewer.Execution.Iterators.RowMode.Joins;
using InternalsViewer.Execution.Iterators.RowMode.Row;
using InternalsViewer.Execution.Iterators.RowMode.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution.Iterators;

public sealed class IteratorFactory(IServiceProvider services) : IIteratorFactory
{
    public IIterator Create(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition
                => services.GetRequiredService<NestedLoopsIterator>(),
            MergeJoinDefinition
                => services.GetRequiredService<MergeJoinIterator>(),
            HashMatchDefinition
                => services.GetRequiredService<HashMatchIterator>(),
            TopDefinition
                => services.GetRequiredService<TopIterator>(),
            SelectDefinition
                => services.GetRequiredService<SelectIterator>(),
            ConcatenationDefinition
                => services.GetRequiredService<ConcatenationIterator>(),
            SortDefinition
                => services.GetRequiredService<SortIterator>(),
            StreamAggregateDefinition
                => services.GetRequiredService<StreamAggregateIterator>(),
            HashAggregateDefinition
                => services.GetRequiredService<HashAggregateIterator>(),
            ComputeScalarDefinition
                => services.GetRequiredService<ComputeScalarIterator>(),
            FilterDefinition
                => services.GetRequiredService<FilterIterator>(),
            SegmentDefinition
                => services.GetRequiredService<SegmentIterator>(),
            SequenceProjectDefinition
                => services.GetRequiredService<SequenceProjectIterator>(),
            AllocationScanDefinition
                => services.GetRequiredService<AllocationScanIterator>(),
            HeapFetchDefinition
                => services.GetRequiredService<HeapFetchIterator>(),
            RangeDefinition or SeekDefinition
                => services.GetRequiredService<IndexIterator>(),
            _ => throw new ArgumentException($"No iterator runs a {definition.GetType().Name}")
        };
}
