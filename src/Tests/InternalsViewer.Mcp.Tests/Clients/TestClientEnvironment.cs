using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Tests.Clients;

internal sealed class TestClientEnvironment : IDisposable
{
    public TestClientEnvironment(params string[] executables)
    {
        Root = Path.Combine(Path.GetTempPath(), "InternalsViewerMcpTests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Home);

        Directory.CreateDirectory(ApplicationData);

        Environment = new McpClientEnvironment(Home,
                                               ApplicationData,
                                               name => executables.Contains(name) ? Path.Combine(Root, $"{name}.exe") : null);
    }

    public string Root { get; }

    public string Home => Path.Combine(Root, "home");

    public string ApplicationData => Path.Combine(Root, "appdata");

    public McpClientEnvironment Environment { get; }

    public string CreateDirectory(params string[] parts)
    {
        var path = Path.Combine([Root, .. parts]);

        Directory.CreateDirectory(path);

        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class FakeCommandRunner : ICommandRunner
{
    public List<(string Executable, string[] Arguments)> Calls { get; } = [];

    public int ExitCode { get; set; }

    public string Output { get; set; } = string.Empty;

    public Task<CommandResult> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        Calls.Add((executable, arguments));

        return Task.FromResult(new CommandResult(ExitCode, Output));
    }
}
