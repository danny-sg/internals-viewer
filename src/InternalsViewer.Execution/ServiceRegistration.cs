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

        services.AddTransient<IndexStepIterator>();
        services.AddTransient<AllocationStepIterator>();
        services.AddTransient<NestedLoopsStepIterator>();
        services.AddTransient<MergeJoinStepIterator>();
        services.AddTransient<HashMatchStepIterator>();
        services.AddTransient<HeapFetchStepIterator>();
        services.AddTransient<TopIterator>();
    }
}
