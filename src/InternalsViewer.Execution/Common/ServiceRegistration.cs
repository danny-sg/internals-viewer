using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Execution.Common.Interfaces;
using InternalsViewer.Execution.Common.Iterators;
using InternalsViewer.Execution.BatchMode.Iterators;
using InternalsViewer.Execution.BatchMode.Iterators.DataAccess;
using InternalsViewer.Execution.Common.Iterators.Common;
using InternalsViewer.Execution.RowMode.Iterators.Aggregation;
using InternalsViewer.Execution.RowMode.Iterators.DataAccess;
using InternalsViewer.Execution.RowMode.Iterators.Joins;
using InternalsViewer.Execution.RowMode.Iterators.Row;
using InternalsViewer.Execution.RowMode.Iterators.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution.Common;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterExecutionServices(this IServiceCollection services)
    {
        services.AddSingleton<IIteratorFactory, IteratorFactory>();

        services.AddTransient<IndexIterator>();
        services.AddTransient<AllocationScanIterator>();
        services.AddTransient<NestedLoopsIterator>();
        services.AddTransient<MergeJoinIterator>();
        services.AddTransient<HashMatchIterator>();
        services.AddTransient<HeapFetchIterator>();
        services.AddTransient<TopIterator>();
        services.AddTransient<SelectIterator>();
        services.AddTransient<ConcatenationIterator>();
        services.AddTransient<SortIterator>();
        services.AddTransient<StreamAggregateIterator>();
        services.AddTransient<HashAggregateIterator>();
        services.AddTransient<ComputeScalarIterator>();
        services.AddTransient<FilterIterator>();
        services.AddTransient<SegmentIterator>();
        services.AddTransient<SequenceProjectIterator>();

        services.AddTransient<ColumnstoreScanIterator>();
        services.AddTransient<BatchToRowIterator>();

        services.AddTransient<BatchFilterIterator>();

        services.AddTransient<RowToBatchIterator>();

        services.AddTransient<BatchComputeScalarIterator>();

        services.AddTransient<BatchHashAggregateIterator>();
    }
}
