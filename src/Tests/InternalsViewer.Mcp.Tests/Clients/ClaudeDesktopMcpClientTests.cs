using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Clients.Claude;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Tests.Clients;

[Trait("Category", "Unit")]
public class ClaudeDesktopMcpClientTests
{
    private static readonly McpHostSettings Settings = new(8484, "secret");

    [Fact]
    public async Task Not_Installed_Without_The_Claude_Folder()
    {
        using var environment = new TestClientEnvironment("npx");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        Assert.False(client.IsInstalled);
        Assert.Equal(McpClientStatus.NotInstalled, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Needs_Node_Before_It_Can_Register()
    {
        using var environment = new TestClientEnvironment();

        environment.CreateDirectory("appdata", "Claude");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        var state = await client.GetStateAsync(Settings, CancellationToken.None);

        Assert.True(client.IsInstalled);
        Assert.Equal(McpClientStatus.NotInstalled, state.Status);
        Assert.Contains("Node.js", state.Detail);
        Assert.Equal(ClaudeDesktopMcpClient.SetupUrl, client.HelpUrl);
    }

    [Fact]
    public async Task Registration_Writes_The_Bridge_Command_With_The_Token_In_Its_Environment()
    {
        using var environment = new TestClientEnvironment("npx");

        var folder = environment.CreateDirectory("appdata", "Claude");

        var path = Path.Combine(folder, "claude_desktop_config.json");

        await File.WriteAllTextAsync(path, """{ "mcpServers": { "other": { "command": "other.exe" } } }""");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

        var entry = root["mcpServers"]!["internals-viewer"]!;

        var arguments = entry["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToArray();

        Assert.Equal("cmd", entry["command"]!.GetValue<string>());
        Assert.Equal(["/c", "npx", "-y", "mcp-remote", "http://127.0.0.1:8484/mcp", "--header",
                      "Authorization: Bearer ${INTERNALS_VIEWER_TOKEN}"],
                     arguments);
        Assert.Equal("secret", entry["env"]!["INTERNALS_VIEWER_TOKEN"]!.GetValue<string>());
        Assert.Equal("other.exe", root["mcpServers"]!["other"]!["command"]!.GetValue<string>());
        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task A_Changed_Token_Or_Port_Needs_An_Update()
    {
        using var environment = new TestClientEnvironment("npx");

        var folder = environment.CreateDirectory("appdata", "Claude");

        await File.WriteAllTextAsync(Path.Combine(folder, "claude_desktop_config.json"), "{}");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var rotated = await client.GetStateAsync(Settings with { Token = "rotated" }, CancellationToken.None);

        var moved = await client.GetStateAsync(Settings with { Port = 9000 }, CancellationToken.None);

        Assert.Equal(McpClientStatus.NeedsUpdate, rotated.Status);
        Assert.Equal(McpClientStatus.NeedsUpdate, moved.Status);

        await client.UnregisterAsync(CancellationToken.None);

        Assert.Equal(McpClientStatus.NotRegistered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task A_Packaged_Install_Registers_In_Its_Own_Data_Folder()
    {
        using var environment = new TestClientEnvironment("npx");

        var roaming = environment.CreateDirectory("appdata", "Claude");

        var packaged = environment.CreateDirectory("local", "Claude-Data", "claude_package");

        var path = Path.Combine(packaged, "claude_desktop_config.json");

        await File.WriteAllTextAsync(path, """{ "preferences": { "keep": true } }""");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

        Assert.NotNull(root["mcpServers"]!["internals-viewer"]);
        Assert.True(root["preferences"]!["keep"]!.GetValue<bool>());
        Assert.False(File.Exists(Path.Combine(roaming, "claude_desktop_config.json")));
        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Asks_For_Its_Config_Before_Registering()
    {
        using var environment = new TestClientEnvironment("npx");

        var packaged = environment.CreateDirectory("local", "Claude-Data", "claude_package");

        var client = new ClaudeDesktopMcpClient(environment.Environment);

        var state = await client.GetStateAsync(Settings, CancellationToken.None);

        await Assert.ThrowsAsync<McpClientException>(() => client.RegisterAsync(Settings, CancellationToken.None));

        Assert.True(client.IsInstalled);
        Assert.Equal(McpClientStatus.NotRegistered, state.Status);
        Assert.Contains("Edit Config", state.Detail);
        Assert.False(File.Exists(Path.Combine(packaged, "claude_desktop_config.json")));
    }
}
