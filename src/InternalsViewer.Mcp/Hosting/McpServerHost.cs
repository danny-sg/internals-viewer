using System.Security.Cryptography;
using System.Text;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Interfaces.Guides;
using InternalsViewer.Mcp.Prompts;
using InternalsViewer.Mcp.Prompts.Indexes;
using InternalsViewer.Mcp.Prompts.Queries;
using InternalsViewer.Mcp.Resources.Queries;
using InternalsViewer.Mcp.Tools;
using InternalsViewer.Mcp.Tools.Allocations;
using InternalsViewer.Mcp.Tools.Columnstore;
using InternalsViewer.Mcp.Tools.Indexes;
using InternalsViewer.Mcp.Tools.Pages;
using InternalsViewer.Mcp.Tools.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace InternalsViewer.Mcp.Hosting;

public sealed class McpServerHost(IServiceProvider services, ILogger<McpServerHost> logger) : IAsyncDisposable
{
    public bool IsRunning => Application is not null;

    public McpHostSettings? Settings { get; private set; }

    private IServiceProvider Services { get; } = services;

    private ILogger<McpServerHost> Logger { get; } = logger;

    private WebApplication? Application { get; set; }

    private SemaphoreSlim Sync { get; } = new(1, 1);

    public static string CreateToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    public async Task StartAsync(McpHostSettings settings, CancellationToken cancellationToken)
    {
        await Sync.WaitAsync(cancellationToken);

        try
        {
            await StopCoreAsync();

            var application = Build(settings);

            await application.StartAsync(cancellationToken);

            Application = application;

            Settings = settings;

            Logger.LogInformation("MCP server listening on {Endpoint}", settings.Endpoint);
        }
        finally
        {
            Sync.Release();
        }
    }

    public async Task StopAsync()
    {
        await Sync.WaitAsync();

        try
        {
            await StopCoreAsync();
        }
        finally
        {
            Sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        Sync.Dispose();
    }

    private WebApplication Build(McpHostSettings settings)
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(Services.GetRequiredService<ILoggerFactory>());

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(settings.Port));

        builder.Services.AddSingleton(Services.GetRequiredService<IMcpTabSource>());

        if (Services.GetService<IMcpWorkspace>() is { } workspace)
        {
            builder.Services.AddSingleton(workspace);
        }

        builder.Services.AddSingleton(Services.GetRequiredService<McpDocumentResolver>());

        foreach (var source in Services.GetServices<IMcpGuideSource>())
        {
            builder.Services.AddSingleton(source);
        }

        builder.Services
               .AddMcpServer(options =>
               {
                   options.ServerInfo = new Implementation
                   {
                       Name = "internals-viewer",
                       Title = "Internals Viewer",
                       Version = typeof(McpServerHost).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"
                   };

                   options.ServerInstructions = ServerInstructions.Text;
               })
               .WithHttpTransport(http => http.Stateless = true)
               .WithTools<TabTools>()
               .WithTools<DocumentTools>()
               .WithTools<NavigationTools>()
               .WithTools<RunTools>()
               .WithTools<EventTools>()
               .WithTools<QueryOptionTools>()
               .WithTools<CallTreeTools>()
               .WithTools<CallLogTools>()
               .WithTools<FunctionSearchTools>()
                   .WithTools<TraceTimelineTools>()
               .WithTools<TimelineTools>()
               .WithTools<MemoryTools>()
               .WithTools<PageTools>()
               .WithTools<PageCommandTools>()
               .WithTools<QueryPageTools>()
               .WithTools<IndexCommandTools>()
               .WithTools<AllocationTools>()
               .WithTools<AllocationCommandTools>()
               .WithTools<IndexTools>()
               .WithTools<ColumnstoreTools>()
               .WithTools<ColumnstoreCommandTools>()
               .WithTools<GuideTools>()
               .WithResources<DocumentResources>()
               .WithPrompts<ExplorePrompts>()
               .WithPrompts<IndexPrompts>();

        var application = builder.Build();

        var token = Encoding.UTF8.GetBytes(settings.Token);

        application.Use(async (context, next) =>
        {
            if (!IsLocalHost(context.Request.Host.Host) || !IsAuthorised(context.Request, token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsync("Internals Viewer MCP server: a valid bearer token is required.");

                return;
            }

            await next(context);
        });

        application.MapMcp(McpHostSettings.Path);

        return application;
    }

    private async Task StopCoreAsync()
    {
        if (Application is not { } application)
        {
            return;
        }

        Application = null;

        Settings = null;

        try
        {
            await application.StopAsync();
        }
        finally
        {
            await application.DisposeAsync();
        }

        Logger.LogInformation("MCP server stopped");
    }

    private static bool IsLocalHost(string host)
        => host is "localhost" or "127.0.0.1" or "[::1]" or "::1" || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthorised(HttpRequest request, byte[] token)
    {
        if (request.Headers.Authorization.Count != 1)
        {
            return false;
        }

        var header = request.Headers.Authorization[0] ?? string.Empty;

        const string scheme = "Bearer ";

        if (!header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(header[scheme.Length..].Trim());

        return CryptographicOperations.FixedTimeEquals(presented, token);
    }
}
