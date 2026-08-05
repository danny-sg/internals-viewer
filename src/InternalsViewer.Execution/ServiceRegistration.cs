using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators;
using InternalsViewer.Execution.Iterators.DataAccess;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Row;
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
    }
}
