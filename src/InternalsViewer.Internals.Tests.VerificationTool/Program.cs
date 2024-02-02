using InternalsViewer.Internals.Tests.VerificationTool.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.VerificationTool;

internal static class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddTransient<ObjectPageListService>();
        builder.Services.AddTransient<IndexVerificationService>();

        builder.Services.RegisterServices();

        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        using var host = builder.Build();

        using var scope = host.Services.CreateScope();

        var services = scope.ServiceProvider;

        var service = services.GetRequiredService<IndexVerificationService>();

        await service.VerifyIndex(709577566, 3);
    }
}
