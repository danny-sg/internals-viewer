using System.Net;
using System.Net.Sockets;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace InternalsViewer.Mcp.Tests.Hosting;

[Trait("Category", "Unit")]
public class McpServerHostTests
{
    [Fact]
    public async Task Serves_The_Tools_Over_Http_To_A_Client_With_The_Token()
    {
        await using var provider = CreateServices();

        var host = provider.GetRequiredService<McpServerHost>();

        var settings = new McpHostSettings(GetFreePort(), McpServerHost.CreateToken());

        await host.StartAsync(settings, CancellationToken.None);

        try
        {
            Assert.True(host.IsRunning);

            await using var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(settings.Endpoint),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {settings.Token}" }
            });

            await using var client = await McpClient.CreateAsync(transport);

            var tools = await client.ListToolsAsync();

            Assert.Contains(tools, t => t.Name == "list_tabs");
            Assert.Contains(tools, t => t.Name == "show_call");

            var result = await client.CallToolAsync("list_tabs");

            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

            Assert.Contains("q1", text);
            Assert.Contains("Sales", text);
        }
        finally
        {
            await host.StopAsync();
        }

        Assert.False(host.IsRunning);
    }

    [Fact]
    public async Task Rejects_A_Request_Without_The_Token()
    {
        await using var provider = CreateServices();

        var host = provider.GetRequiredService<McpServerHost>();

        var settings = new McpHostSettings(GetFreePort(), McpServerHost.CreateToken());

        await host.StartAsync(settings, CancellationToken.None);

        try
        {
            using var http = new HttpClient();

            var response = await http.PostAsync(settings.Endpoint, new StringContent("{}"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            http.DefaultRequestHeaders.Authorization = new("Bearer", "wrong");

            var wrong = await http.PostAsync(settings.Endpoint, new StringContent("{}"));

            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<IMcpTabSource>(new TestDocumentSource(new TestDocument { Run = TestRuns.Create() }));

        services.RegisterMcpServices();

        return services.BuildServiceProvider();
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
