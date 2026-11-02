using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

public sealed class McpDocumentScope(
    IMcpTab tab,
    QueryContext query,
    QueryExportContent run,
    DocumentContext context)
{
    public QueryContext Query { get; } = query;

    public QueryExportContent Run { get; } = run;

    public DocumentContext Context { get; } = context;

    public McpSelection Selection => Query.Selection;

    public QueryResult Result => Run.Result;

    public TraceResult? Trace => Context.Trace;

    public long CropStartUs => Result.CropStartUs ?? 0;

    private IMcpTab Tab { get; } = tab;

    public TraceResult RequireTrace()
        => Trace
           ?? throw new McpException($"Document {Query.TabId} has no Full Trace. Only the events, plan and call stacks are available.");

    public void RequireOperatorFrames()
    {
        if (Query.IsTraceLoading)
        {
            throw new McpException("The Full Trace is still loading. The events, plan, reads, waits, locks and latches " +
                                   "are already available.");
        }
    }

    public HashSet<CallStackNode>? GetCropRoots()
        => Trace is { QueryRoots.Count: > 0 } trace && Query.Options.Any(o => o is { Key: QueryCrop.OptionKey, IsOn: true })
            ? new HashSet<CallStackNode>(trace.QueryRoots, ReferenceEqualityComparer.Instance)
            : null;

    public async Task<string?> UncropForAsync(List<(CallStackNode Node, int CallIndex)> calls,
                                              List<TracePosition> positions,
                                              CancellationToken cancellationToken)
    {
        if (Trace is not { } trace || GetCropRoots() is not { } roots)
        {
            return null;
        }

        var start = trace.QueryTimeline.Start.Position;

        var end = trace.QueryTimeline.End.Position;

        string? hidden = null;

        TracePosition? at = null;

        foreach (var (node, callIndex) in calls)
        {
            if (!QueryCrop.IsHidden(node, roots))
            {
                continue;
            }

            hidden = callIndex >= 0 ? $"{Context.GetFunctionName(node)} #{callIndex}" : Context.GetFunctionName(node);

            at = node.Frame is { } frame
                 && trace.CallStore.GetCallList(frame.Address, frame.InstanceAddress) is { } list
                 && callIndex >= 0
                 && callIndex < list.Count
                ? list.GetFromPosition(callIndex)
                : null;

            break;
        }

        foreach (var position in positions)
        {
            if (hidden is not null || !QueryCrop.IsHidden(position, start, end))
            {
                continue;
            }

            hidden = $"position {position}";

            at = position;
        }

        if (hidden is null)
        {
            return null;
        }

        await RunAsync(c => c.SetOptionAsync(QueryCrop.OptionKey, false), cancellationToken);

        return $"Turned off Crop To Query so the user can see {hidden}, which runs {QueryCrop.GetPhase(at, start, end)}. " +
               "Tell the user you did this and why. Turn it back on with query_set_option crop-to-query when you move on.";
    }

    public async Task<QueryContext> ReadContextAsync(CancellationToken cancellationToken)
        => await Tab.GetContextAsync(cancellationToken) as QueryContext
           ?? throw new McpException($"Tab {Tab.Id} has no Query context.");

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
    }

    public async Task<TResult> RunAsync<TCommands, TResult>(Func<TCommands, Task<TResult>> command, CancellationToken cancellationToken)
        where TCommands : class
    {
        try
        {
            return await Tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }
    }

    public async Task RunAsync(Func<IQueryCommands, Task> command, CancellationToken cancellationToken)
    {
        try
        {
            await Tab.RunAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new McpException(exception.Message);
        }
    }
}
