using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.Services.Mcp;

internal sealed class QueryMcpDocument : IMcpDocument, IMcpNavigator
{
    public QueryMcpDocument(string id, QueryViewModel viewModel, bool isActive, DispatcherQueue dispatcher)
    {
        Id = id;
        ViewModel = viewModel;
        Dispatcher = dispatcher;
        IsActive = isActive;
        Title = viewModel.Name;
        ServerName = viewModel.Database.Connection.Name;
        DatabaseName = viewModel.Database.Name;
        SqlText = viewModel.Sql;
        Run = viewModel.CurrentRun;
        Database = viewModel.Database;
    }

    public string Id { get; }

    public string Title { get; }

    public string ServerName { get; }

    public string DatabaseName { get; }

    public bool IsActive { get; }

    public string SqlText { get; }

    public QueryExportContent? Run { get; }

    public DatabaseSource? Database { get; }

    public IMcpNavigator? Navigator => this;

    private QueryViewModel ViewModel { get; }

    private DispatcherQueue Dispatcher { get; }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => RunAsync(() => GetWindow().ActivateTab(ViewModel), cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => RunAsync(() => GetWindow().CloseTab(ViewModel), cancellationToken);

    public Task ShowCallAsync(CallStackNode node, int callIndex, CancellationToken cancellationToken)
        => RunAsync(() => ViewModel.OpenCallTree(node, callIndex), cancellationToken);

    public Task ShowPageAsync(PageAddress pageAddress, ushort? slot, CancellationToken cancellationToken)
        => RunAsync(() => ViewModel.OpenPage(pageAddress, slot), cancellationToken);

    public Task ShowPlanNodeAsync(PlanNodeIdentifier identifier, CancellationToken cancellationToken)
        => RunAsync(() => ViewModel.OpenExecutionPlan(identifier), cancellationToken);

    public Task ShowEventAsync(EngineEvent engineEvent, CancellationToken cancellationToken)
        => RunAsync(() => ViewModel.NavigateToEvent(engineEvent), cancellationToken);

    public Task SetPlayheadAsync(TracePosition position, CancellationToken cancellationToken)
        => RunAsync(() =>
        {
            ViewModel.Layout.ShowTraceTimeline();

            var instructions = Run?.ExtraOf<TraceResult>()?.Timeline.Threads.Select(t => t.Clock.GetInstructions(position)).DefaultIfEmpty().Max() ?? 0;

            ViewModel.TraceTimelineTab.RequestPlayhead(new TracePoint(position, instructions));
        }, cancellationToken);

    public Task ShowMemoryAsync(int block, ulong address, TracePosition position, CancellationToken cancellationToken)
        => RunAsync(() => ViewModel.TraceTimelineTab.OpenMemory(block, address, position), cancellationToken);

    public Task<McpSelection> GetSelectionAsync(CancellationToken cancellationToken)
        => RunAsync(() => new McpSelection
        {
            Event = ViewModel.SelectedEvent,
            PlanNode = ViewModel.SelectedPlanNode,
            CallNode = ViewModel.SelectedCallNode,
            Playhead = ViewModel.TraceTimeline is null ? null : ViewModel.TraceTimelineTab.Playhead,
            PlayheadTimeUs = ViewModel.PlayheadTimeUs == 0 ? null : ViewModel.PlayheadTimeUs,
            TraceRange = ViewModel.TraceTimeline is null ? null : GetTraceRange(ViewModel.TraceTimelineTab.ViewRange),
            EventRange = GetEventRange(ViewModel.VisibleTimeRangeSource?.Invoke()),
            Scope = ViewModel is { StartOffset: { } start, EndOffset: { } end } ? new McpTimeRange(start, end) : null,
            Pages = [.. ViewModel.GetOpenPages().Select(p => GetPageSelection(p.Page, p.IsActive))],
            VisibleViews = [.. ViewModel.Layout.VisibleKeys]
        }, cancellationToken);

    private static McpTimeRange? GetEventRange((long StartUs, long EndUs)? window)
        => window is { } visible ? new McpTimeRange(visible.StartUs, visible.EndUs) : null;

    private static McpTraceRange? GetTraceRange(TraceViewRange? range)
        => range is null ? null : new McpTraceRange(range.Axis.ToString(), range.Start, range.End, range.From, range.To);

    private static MainWindow GetWindow()
        => App.MainWindow ?? throw new InvalidOperationException("The Internals Viewer window is not available.");

    private static McpPageSelection GetPageSelection(PageTabViewModel page, bool isActive)
        => new(page.PageAddress,
               isActive,
               page.SelectedSlot?.Index,
               page.SelectedMarker?.Name,
               page.SelectedMarker?.StartPosition,
               page.SelectedLogRecord is { } record ? $"{record.Lsn} {record.Operation}" : null);

    private Task RunAsync(Action action, CancellationToken cancellationToken)
        => RunAsync(() =>
        {
            action();

            return true;
        }, cancellationToken);

    private Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = Dispatcher.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        if (!queued)
        {
            completion.SetException(new InvalidOperationException("Internals Viewer is closing."));
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}
