using InternalsViewer.Mcp.Clients;

namespace InternalsViewer.Mcp.Interfaces.Clients;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken);
}
