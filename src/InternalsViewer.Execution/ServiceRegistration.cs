using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Execution.Services.Allocations;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Execution.Services.Joins;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterExecutionServices(this IServiceCollection services)
    {
        services.AddTransient<IndexStepService>();
        services.AddTransient<AllocationStepService>();
        services.AddTransient<NestedLoopsStepService>();
        services.AddTransient<MergeJoinStepService>();
    }
}
