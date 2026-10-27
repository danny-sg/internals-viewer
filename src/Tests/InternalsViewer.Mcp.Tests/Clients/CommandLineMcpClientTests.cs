using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Clients.Claude;
using InternalsViewer.Mcp.Clients.Copilot;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Tests.Clients;

[Trait("Category", "Unit")]
public class CommandLineMcpClientTests
{
    private static readonly McpHostSettings Settings = new(8484, "secret");

    [Fact]
    public async Task Claude_Code_Is_Not_Installed_Without_The_Executable()
    {
        using var environment = new TestClientEnvironment();

        var client = new ClaudeCodeMcpClient(environment.Environment, new FakeCommandRunner());

        Assert.False(client.IsInstalled);
        Assert.Equal(McpClientStatus.NotInstalled, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);
        await Assert.ThrowsAsync<McpClientException>(() => client.RegisterAsync(Settings, CancellationToken.None));
    }

    [Fact]
    public async Task Claude_Code_Registers_At_User_Scope_With_The_Header()
    {
        using var environment = new TestClientEnvironment("claude");

        var runner = new FakeCommandRunner();

        var client = new ClaudeCodeMcpClient(environment.Environment, runner);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var (executable, arguments) = Assert.Single(runner.Calls);

        Assert.EndsWith("claude.exe", executable);
        Assert.Equal(["mcp", "add", "--transport", "http", "-s", "user", "internals-viewer", "http://127.0.0.1:8484/mcp",
                      "--header", "Authorization: Bearer secret"],
                     arguments);
    }

    [Fact]
    public async Task Claude_Code_Removes_An_Existing_Registration_Before_Adding()
    {
        using var environment = new TestClientEnvironment("claude");

        await File.WriteAllTextAsync(environment.Environment.GetHomePath(".claude.json"), """
            { "mcpServers": { "internals-viewer": { "type": "http", "url": "http://127.0.0.1:8484/mcp",
                                                    "headers": { "Authorization": "Bearer old" } } } }
            """);

        var runner = new FakeCommandRunner();

        var client = new ClaudeCodeMcpClient(environment.Environment, runner);

        Assert.Equal(McpClientStatus.NeedsUpdate, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);

        await client.RegisterAsync(Settings, CancellationToken.None);

        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(["mcp", "remove", "-s", "user", "internals-viewer"], runner.Calls[0].Arguments);
        Assert.Equal("add", runner.Calls[1].Arguments[1]);
    }

    [Fact]
    public async Task A_Failing_Command_Reports_Its_Output()
    {
        using var environment = new TestClientEnvironment("claude");

        var runner = new FakeCommandRunner { ExitCode = 1, Output = "No MCP server found with name: internals-viewer" };

        var client = new ClaudeCodeMcpClient(environment.Environment, runner);

        var exception = await Assert.ThrowsAsync<McpClientException>(() => client.UnregisterAsync(CancellationToken.None));

        Assert.Contains("No MCP server found", exception.Message);
    }

    [Fact]
    public async Task Copilot_Cli_Registers_And_Reads_Its_User_Configuration()
    {
        using var environment = new TestClientEnvironment("copilot");

        var runner = new FakeCommandRunner();

        var client = new CopilotCliMcpClient(environment.Environment, runner);

        await client.RegisterAsync(Settings, CancellationToken.None);

        var (_, arguments) = Assert.Single(runner.Calls);

        Assert.Equal(["mcp", "add", "--transport", "http", "internals-viewer", "http://127.0.0.1:8484/mcp",
                      "--header", "Authorization: Bearer secret"],
                     arguments);

        environment.CreateDirectory("home", ".copilot");

        await File.WriteAllTextAsync(environment.Environment.GetHomePath(".copilot", "mcp-config.json"), """
            { "mcpServers": { "internals-viewer": { "type": "http", "url": "http://127.0.0.1:8484/mcp",
                                                    "headers": { "Authorization": "Bearer secret" } } } }
            """);

        Assert.Equal(McpClientStatus.Registered, (await client.GetStateAsync(Settings, CancellationToken.None)).Status);

        await client.UnregisterAsync(CancellationToken.None);

        Assert.Equal(["mcp", "remove", "internals-viewer"], runner.Calls[^1].Arguments);
    }
}
