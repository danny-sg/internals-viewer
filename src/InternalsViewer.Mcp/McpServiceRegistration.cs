using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Clients.Claude;
using InternalsViewer.Mcp.Clients.Copilot;
using InternalsViewer.Mcp.Clients.Cursor;
using InternalsViewer.Mcp.Clients.VsCode;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Guides;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;
using InternalsViewer.Mcp.Interfaces.Guides;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Mcp;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterMcpServices(this IServiceCollection services)
    {
        services.AddSingleton<McpDocumentResolver>();

        services.AddSingleton<McpServerHost>();

        services.AddSingleton(McpClientEnvironment.Default);

        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();

        services.AddSingleton<IMcpGuideSource, EmbeddedGuideSource>();

        RegisterClients(services);
    }

    private static void RegisterClients(IServiceCollection services)
    {
        services.AddSingleton<IMcpClient, ClaudeCodeMcpClient>();

        services.AddSingleton<IMcpClient, CursorMcpClient>();

        services.AddSingleton<IMcpClient, CopilotCliMcpClient>();

        services.AddSingleton<IMcpClient, VsCodeMcpClient>();

        services.AddSingleton<IMcpClient, ClaudeDesktopMcpClient>();
    }
}
