using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

/// <summary>
/// One tab resolved once, so the context read before a command and the context read after it come from the same tab
/// </summary>
public sealed class McpAreaSession<TContext>(IMcpTab tab, TContext before)
    where TContext : TabContext, IAreaContext
{
    public IMcpTab Tab { get; } = tab;

    public TContext Before { get; } = before;

    public TContext Context { get; private set; } = before;

    public async Task RunAsync<TCommands>(Func<TCommands, Task> command, CancellationToken cancellationToken)
        where TCommands : class
    {
        try
        {
            await Tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task<TResult> RunAsync<TCommands, TResult>(
        Func<TCommands, Task<TResult>> command,
        CancellationToken cancellationToken)
        where TCommands : class
    {
        TResult result;

        try
        {
            result = await Tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }

        await RefreshAsync(cancellationToken);

        return result;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Context = await Tab.GetContextAsync(cancellationToken) as TContext
                  ?? throw new McpException($"Tab {Tab.Id} has no {TContext.Area} context.");
    }
}
