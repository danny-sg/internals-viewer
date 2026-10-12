using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Export;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Query.FullTrace;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterFullTraceServices(this IServiceCollection services)
    {
        services.AddTransient<FullTraceLoader>();

        services.AddSingleton<IRunExportSection, FullTraceExportSection>();
    }
}
