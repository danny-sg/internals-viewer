using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Iterators.BatchMode;
using InternalsViewer.Execution.Iterators.BatchMode.DataAccess;
using InternalsViewer.Execution.Iterators.Common;
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
