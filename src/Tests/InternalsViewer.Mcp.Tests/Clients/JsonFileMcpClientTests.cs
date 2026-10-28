using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Clients.Cursor;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Clients.VsCode;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Tests.Clients;

[Trait("Category", "Unit")]
public class JsonFileMcpClientTests
{
    private static readonly McpHostSettings Settings = new(8484, "secret");

    [Fact]
    public async Task Cursor_Is_Not_Installed_Without_Its_Folder()
    {
        using var environment = new TestClientEnvironment();

        var client = new CursorMcpClient(environment.Environment);

        Assert.False(client.IsInstalled);
        Assert.Equal(McpClientStatus.NotInstalled, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Cursor_Registration_Writes_The_Server_And_Keeps_Other_Servers()
    {
        using var environment = new TestClientEnvironment();

        var folder = environment.CreateDirectory("home", ".cursor");

        var path = Path.Combine(folder, "mcp.json");

        await File.WriteAllTextAsync(path, """{ "mcpServers": { "other": { "url": "http://example/mcp" } }, "keep": true }""");

        var client = new CursorMcpClient(environment.Environment);

        Assert.Equal(McpClientStatus.NotRegistered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

        var entry = root["mcpServers"]!["internals-viewer"]!;

        Assert.Equal("http://127.0.0.1:8484/mcp", entry["url"]!.GetValue<string>());
        Assert.Equal("Bearer secret", entry["headers"]!["Authorization"]!.GetValue<string>());
        Assert.Null(entry["type"]);
        Assert.Equal("http://example/mcp", root["mcpServers"]!["other"]!["url"]!.GetValue<string>());
        Assert.True(root["keep"]!.GetValue<bool>());
        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task A_Changed_Token_Or_Port_Needs_An_Update()
    {
        using var environment = new TestClientEnvironment();

        environment.CreateDirectory("home", ".cursor");

        var client = new CursorMcpClient(environment.Environment);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var rotated = await client.GetStateAsync(Settings with { Token = "rotated" }, CancellationToken.None);

        var moved = await client.GetStateAsync(Settings with { Port = 9000 }, CancellationToken.None);

        Assert.Equal(McpClientStatus.NeedsUpdate, rotated.Status);
        Assert.Equal(McpClientStatus.NeedsUpdate, moved.Status);
    }

    [Fact]
    public async Task Unregistering_Removes_Only_The_Server()
    {
        using var environment = new TestClientEnvironment();

        var folder = environment.CreateDirectory("home", ".cursor");

        var path = Path.Combine(folder, "mcp.json");

        await File.WriteAllTextAsync(path, """{ "mcpServers": { "other": { "url": "http://example/mcp" } } }""");

        var client = new CursorMcpClient(environment.Environment);

        await client.RegisterAsync(Settings, CancellationToken.None);

        await client.UnregisterAsync(CancellationToken.None);

        var servers = JsonNode.Parse(await File.ReadAllTextAsync(path))!["mcpServers"]!.AsObject();

        Assert.False(servers.ContainsKey("internals-viewer"));
        Assert.True(servers.ContainsKey("other"));
        Assert.Equal(McpClientStatus.NotRegistered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Visual_Studio_Code_Writes_A_Typed_Entry_Under_Servers()
    {
        using var environment = new TestClientEnvironment();

        var folder = environment.CreateDirectory("appdata", "Code", "User");

        var path = Path.Combine(folder, "mcp.json");

        await File.WriteAllTextAsync(path, "{}");

        var client = new VsCodeMcpClient(environment.Environment);

        Assert.True(client.IsInstalled);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

        var entry = root["servers"]!["internals-viewer"]!;

        Assert.Equal("http", entry["type"]!.GetValue<string>());
        Assert.Equal("http://127.0.0.1:8484/mcp", entry["url"]!.GetValue<string>());
        Assert.Equal("Bearer secret", entry["headers"]!["Authorization"]!.GetValue<string>());
        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Visual_Studio_Code_Asks_For_Its_Config_Before_Registering()
    {
        using var environment = new TestClientEnvironment();

        var folder = environment.CreateDirectory("appdata", "Code", "User");

        var client = new VsCodeMcpClient(environment.Environment);

        var state = await client.GetStateAsync(Settings, CancellationToken.None);

        await Assert.ThrowsAsync<McpClientException>(() => client.RegisterAsync(Settings, CancellationToken.None));

        Assert.Equal(McpClientStatus.NotRegistered, state.Status);
        Assert.Contains("MCP: Open User Configuration", state.Detail);
        Assert.False(File.Exists(Path.Combine(folder, "mcp.json")));
    }

    [Fact]
    public async Task A_Config_With_Comments_Is_Read()
    {
        using var environment = new TestClientEnvironment();

        var folder = environment.CreateDirectory("appdata", "Code", "User");

        await File.WriteAllTextAsync(Path.Combine(folder, "mcp.json"), """
            {
              // user servers
              "servers": { "internals-viewer": { "type": "http", "url": "http://127.0.0.1:8484/mcp",
                                                 "headers": { "Authorization": "Bearer secret" } }, },
            }
            """);

        var client = new VsCodeMcpClient(environment.Environment);

        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }
}
