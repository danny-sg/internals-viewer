using System.Diagnostics;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.Mcp.Clients;

public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public async Task<CommandResult> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var isScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                       || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        if (isScript)
        {
            start.FileName = "cmd.exe";

            start.ArgumentList.Add("/c");

            start.ArgumentList.Add(executable);
        }
        else
        {
            start.FileName = executable;
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new McpClientException($"{executable} could not be started.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(Timeout);

        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);

        var error = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            throw new McpClientException($"{Path.GetFileName(executable)} did not finish within {Timeout.TotalSeconds} seconds.");
        }

        return new CommandResult(process.ExitCode, string.Concat(await output, await error).Trim());
    }
}
