using InternalsViewer.Execution.RowMode.AccessPaths.Definitions;
using InternalsViewer.Execution.Common.Interfaces;
using InternalsViewer.Execution.BatchMode.Interfaces;
using InternalsViewer.Execution.BatchMode.Iterators;
using InternalsViewer.Execution.BatchMode.Iterators.DataAccess;
using InternalsViewer.Execution.Common.Iterators.Common;
using InternalsViewer.Execution.RowMode.Iterators.Aggregation;
using InternalsViewer.Execution.RowMode.Iterators.DataAccess;
using InternalsViewer.Execution.RowMode.Iterators.Joins;
using InternalsViewer.Execution.RowMode.Iterators.Row;
using InternalsViewer.Execution.RowMode.Iterators.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution.Common.Iterators;

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
            BatchToRowDefinition
                => services.GetRequiredService<BatchToRowIterator>(),
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

    public IBatchIterator CreateBatch(IteratorDefinition definition)
        => definition switch
        {
            ColumnstoreScanDefinition
                => services.GetRequiredService<ColumnstoreScanIterator>(),
            BatchFilterDefinition
                => services.GetRequiredService<BatchFilterIterator>(),
            RowToBatchDefinition
                => services.GetRequiredService<RowToBatchIterator>(),
            BatchComputeScalarDefinition
                => services.GetRequiredService<BatchComputeScalarIterator>(),
            BatchHashAggregateDefinition
                => services.GetRequiredService<BatchHashAggregateIterator>(),
            _ => throw new ArgumentException($"No batch iterator runs a {definition.GetType().Name}")
        };
}
