using Microsoft.Extensions.Hosting;

namespace InternalsViewer.Internals.Tests.Helpers;

internal class TestServiceHost
{
    private readonly IHost host;

    public TestServiceHost()
    {
        host = GetHost();
    }

    private IHost GetHost()
    {
        return
            Host
                .CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((context, services) =>
                {
                    services.RegisterServices();
                }).Build();
    }

    internal T GetService<T>() where T : class
    {
        var service = host.Services.GetService(typeof(T));

        return service as T;
    }
}