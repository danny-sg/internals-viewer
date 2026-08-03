using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Execution.Iterators.Allocations;
using InternalsViewer.Execution.Iterators.Heaps;
using InternalsViewer.Execution.Iterators.Indexes;
using InternalsViewer.Execution.Iterators.Joins;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterExecutionServices(this IServiceCollection services)
    {
        services.AddTransient<IndexStepIterator>();
        services.AddTransient<AllocationStepIterator>();
        services.AddTransient<NestedLoopsStepIterator>();
        services.AddTransient<MergeJoinStepIterator>();
        services.AddTransient<HashMatchStepIterator>();
        services.AddTransient<HeapFetchStepIterator>();
    }
}
