using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators;
using InternalsViewer.Execution.Iterators.BatchMode;
using InternalsViewer.Execution.Iterators.BatchMode.DataAccess;
using InternalsViewer.Execution.Iterators.RowMode.Aggregation;
using InternalsViewer.Execution.Iterators.RowMode.DataAccess;
using InternalsViewer.Execution.Iterators.RowMode.Joins;
using InternalsViewer.Execution.Iterators.RowMode.Row;
using InternalsViewer.Execution.Iterators.RowMode.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution;

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
    }
}
