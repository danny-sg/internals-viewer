using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Export;
using Microsoft.Extensions.DependencyInjection;
using InternalsViewer.Query.FullTrace.Recording;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterTraceServices(this IServiceCollection services)
    {
        services.AddTransient<TraceLoader>();

        services.AddSingleton<IQueryExportSection, TraceExportSection>();

        services.AddSingleton<ITimeTravelRecorder, TimeTravelRecorder>();

        services.AddTransient<RowBufferPageResolverFactory>();
    }
}
