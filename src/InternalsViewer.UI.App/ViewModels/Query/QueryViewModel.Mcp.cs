using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Recording;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Page;

namespace InternalsViewer.UI.App.ViewModels.Query;

public partial class QueryViewModel : IMcpContextSource, IQueryCommands
{
    private const string FullTraceKey = "full-trace";

    private static readonly McpOptionBinding[] OptionBindings =
    [
        new("sql-editor",
            "SQL Editor",
            "View",
            q => q.Layout.IsSqlEditorVisible,
            (q, v) => q.Layout.IsSqlEditorVisible = v),
        new("allocations",
            "Allocations",
            "View",
            q => q.Layout.IsAllocationsVisible,
            (q, v) => q.Layout.IsAllocationsVisible = v,
            q => q.Layout.CanShowEventViews ? McpOptionUnavailable.None : McpOptionUnavailable.FullTraceLoaded),
        new("execution-plan",
            "Execution Plan",
            "View",
            q => q.Layout.IsExecutionPlanVisible,
            (q, v) => q.Layout.IsExecutionPlanVisible = v),
        new("events",
            "Events",
            "View",
            q => q.Layout.IsEventsVisible,
            (q, v) => q.Layout.IsEventsVisible = v,
            q => q.Layout.CanShowEventViews ? McpOptionUnavailable.None : McpOptionUnavailable.FullTraceLoaded),
        new("call-tree",
            "Call Tree",
            "View",
            q => q.Layout.IsCallTreeVisible,
            (q, v) => q.Layout.IsCallTreeVisible = v),
        new("call-tree-members",
            "Call Tree Members",
            "View",
            q => q.Layout.IsCallTreeMembersVisible,
            (q, v) => q.Layout.IsCallTreeMembersVisible = v),
        new("call-tree-symbols",
            "Call Tree Symbols",
            "View",
            q => q.Layout.IsCallTreeSymbolsVisible,
            (q, v) => q.Layout.IsCallTreeSymbolsVisible = v),
        new("timeline",
            "Timeline",
            "View",
            q => q.Layout.IsTimelineVisible,
            (q, v) => q.Layout.IsTimelineVisible = v,
            q => q.Layout.CanShowEventViews ? McpOptionUnavailable.None : McpOptionUnavailable.FullTraceLoaded),
        new("trace-timeline",
            "Trace Timeline",
            "View",
            q => q.Layout.IsTraceTimelineVisible,
            (q, v) => q.Layout.IsTraceTimelineVisible = v,
            q => q.Layout.IsTrace ? McpOptionUnavailable.None : McpOptionUnavailable.NeedsFullTrace),
        new("simulation",
            "Simulation",
            "View",
            q => q.IsSimulationVisible,
            (q, v) => q.IsSimulationVisible = v),
        new("instructions",
            "Instructions",
            "View",
            q => q.Layout.IsInstructionsVisible,
            (q, v) => q.Layout.IsInstructionsVisible = v),
        new("track-query",
            "Track Query",
            "Query",
            q => q.TrackQuery,
            (q, v) => q.TrackQuery = v),
        new("crop-to-query",
            "Crop To Query",
            "Query",
            q => q.QueryOptions.CropToQuery,
            (q, v) => q.QueryOptions.CropToQuery = v),
        new("include-system-objects",
            "Include System Objects",
            "Query",
            q => q.QueryOptions.IncludeSystemObjects,
            (q, v) => q.QueryOptions.IncludeSystemObjects = v),
        new("lock-read",
            "Read Locks",
            "Events",
            q => q.QueryOptions.ShowReadLocks,
            (q, v) => q.QueryOptions.ShowReadLocks = v),
        new("lock-update",
            "Update Locks",
            "Events",
            q => q.QueryOptions.ShowUpdateLocks,
            (q, v) => q.QueryOptions.ShowUpdateLocks = v),
        new("lock-write",
            "Write Locks",
            "Events",
            q => q.QueryOptions.ShowWriteLocks,
            (q, v) => q.QueryOptions.ShowWriteLocks = v),
        new("lock-schema",
            "Schema Locks",
            "Events",
            q => q.QueryOptions.ShowSchemaLocks,
            (q, v) => q.QueryOptions.ShowSchemaLocks = v),
        new("lock-range",
            "Range Locks",
            "Events",
            q => q.QueryOptions.ShowRangeLocks,
            (q, v) => q.QueryOptions.ShowRangeLocks = v),
        new("lock-bulk",
            "Bulk Locks",
            "Events",
            q => q.QueryOptions.ShowBulkLocks,
            (q, v) => q.QueryOptions.ShowBulkLocks = v),
        new("waits",
            "Waits",
            "Events",
            q => q.QueryOptions.ShowWaits,
            (q, v) => q.QueryOptions.ShowWaits = v),
        new("latches",
            "Latches",
            "Events",
            q => q.QueryOptions.ShowLatches,
            (q, v) => q.QueryOptions.ShowLatches = v),
        new("columnstore",
            "Columnstore",
            "Events",
            q => q.QueryOptions.IncludeColumnstore,
            (q, v) => q.QueryOptions.IncludeColumnstore = v),
        new("memory",
            "Memory",
            "Events",
            q => q.QueryOptions.IncludeMemory,
            (q, v) => q.QueryOptions.IncludeMemory = v),
        new("call-stack",
            "Call Stack",
            "Events",
            q => q.QueryOptions.IncludeCallStack,
            (q, v) => q.QueryOptions.IncludeCallStack = v),
        new(FullTraceKey,
            "Record Full Trace",
            "Trace",
            q => q.QueryOptions.RecordTimeTravel,
            (q, v) => q.QueryOptions.RecordTimeTravel = v,
            q => q.QueryOptions.RecordTimeTravel || !ConsentRequired() ? McpOptionUnavailable.None : McpOptionUnavailable.NeedsConsent)
    ];

    public TabContext GetContext()
        => new QueryContext
        {
            TabId = TabId,
            Title = Name,
            Database = Database,
            Sql = Sql,
            Run = CurrentRun,
            IsTraceLoading = IsTraceLoading,
            Options =
            [
                .. OptionBindings.Select(b => new McpOption(
                    b.Key,
                    b.Label,
                    b.Group,
                    b.Get(this),
                    b.Unavailable?.Invoke(this) ?? McpOptionUnavailable.None))
            ],
            Timeline = TraceTimeline is null ? null : TraceTimelineTab.Controller?.GetState(),
            Selection = new McpSelection
            {
                Event = SelectedEvent,
                PlanNode = SelectedPlanNode,
                CallNode = SelectedCallNode,
                Playhead = TraceTimeline is null ? null : TraceTimelineTab.Playhead,
                PlayheadTimeUs = PlayheadTimeUs == 0 ? null : PlayheadTimeUs,
                TraceRange = TraceTimeline is null ? null : GetTraceRange(TraceTimelineTab.ViewRange),
                EventRange = GetEventRange(VisibleTimeRangeSource?.Invoke()),
                Scope = this is { StartOffset: { } start, EndOffset: { } end } ? new McpTimeRange(start, end) : null,
                Pages = [.. GetOpenPages().Select(p => GetPageSelection(p.Page, p.IsActive))],
                VisibleViews = [.. Layout.VisibleKeys]
            }
        };

    public Task ShowCallAsync(CallStackNode node, int callIndex)
    {
        OpenCallTree(node, callIndex);

        return Task.CompletedTask;
    }

    public Task ShowPageAsync(PageAddress pageAddress, ushort? slot) => OpenPageAsync(pageAddress, slot);

    public (PageTabViewModel Page, bool IsActive) GetOpenPage(PageAddress? address)
    {
        var pages = GetOpenPages();

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("No page is open in this Query tab.");
        }

        if (address is not { } wanted)
        {
            var active = pages.FindIndex(p => p.IsActive);

            return pages[active >= 0 ? active : pages.Count - 1];
        }

        var index = pages.FindIndex(p => p.Page.PageAddress == wanted);

        if (index < 0)
        {
            var open = string.Join(", ", pages.Select(p => p.Page.PageAddress));

            throw new InvalidOperationException($"Page {wanted} is not open in this Query tab. Open pages: {open}.");
        }

        return pages[index];
    }

    public Task ShowPlanNodeAsync(PlanNodeIdentifier identifier)
    {
        OpenExecutionPlan(identifier);

        return Task.CompletedTask;
    }

    public Task ShowEventAsync(EngineEvent engineEvent)
    {
        NavigateToEvent(engineEvent);

        return Task.CompletedTask;
    }

    public Task SetPlayheadAsync(TracePosition position)
    {
        Layout.ShowTraceTimeline();

        var point = CurrentRun?.ExtraOf<TraceResult>()?.Timeline.GetPoint(position) ?? new TracePoint(position, 0);

        TraceTimelineTab.RequestPlayhead(point);

        return Task.CompletedTask;
    }

    public Task ShowMemoryAsync(int block, ulong address, TracePosition position)
    {
        TraceTimelineTab.OpenMemory(block, address, position);

        return Task.CompletedTask;
    }

    public async Task<McpFunctionSignature?> ResolveSignatureAsync(CallstackFrame frame)
    {
        if (Arguments.Lookup is not { } lookup)
        {
            return null;
        }

        var resolved = await lookup.ResolveAsync(frame);

        return resolved is null ? null : new McpFunctionSignature(resolved.Text ?? string.Empty, resolved.Layout);
    }

    public async Task<TraceValueReferences?> FindReferencesAsync(ulong value)
        => Arguments.Lookup is { } lookup ? await lookup.FindReferencesAsync(value, CancellationToken.None) : null;

    public Task SetOptionAsync(string key, bool enabled)
    {
        var binding = OptionBindings.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"No option '{key}'.");

        var unavailable = binding.Unavailable?.Invoke(this) ?? McpOptionUnavailable.None;

        if (unavailable is McpOptionUnavailable.FullTraceLoaded or McpOptionUnavailable.NeedsFullTrace)
        {
            throw new InvalidOperationException($"{binding.Label} cannot be changed now.");
        }

        if (binding.Key == FullTraceKey && enabled)
        {
            RequireFullTraceConsent();
        }

        binding.Set(this, enabled);

        return Task.CompletedTask;
    }

    private static bool ConsentRequired()
        => App.GetService<SettingsViewModel>().ShowTimeTravelWarning;

    private static void RequireFullTraceConsent()
    {
        var recorder = App.GetService<ITimeTravelRecorder>();

        try
        {
            recorder.PrepareRecorder();
        }
        catch (DebuggerException exception)
        {
            throw new InvalidOperationException(exception.Message);
        }

        if (ConsentRequired() || !recorder.IsLicenceAccepted)
        {
            throw new InvalidOperationException("The Full Trace warning and licence have not been accepted.");
        }
    }

    private static McpTimeRange? GetEventRange((long StartUs, long EndUs)? window)
        => window is { } visible ? new McpTimeRange(visible.StartUs, visible.EndUs) : null;

    private static McpTraceRange? GetTraceRange(TraceViewRange? range)
        => range is null ? null : new McpTraceRange(range.Axis.ToString(), range.Start, range.End, range.From, range.To);

    private static McpPageSelection GetPageSelection(PageTabViewModel page, bool isActive)
        => new(page.PageAddress,
               isActive,
               page.SelectedSlot?.Index,
               page.SelectedMarker?.Name,
               page.SelectedMarker?.StartPosition,
               page.SelectedLogRecord is { } record ? $"{record.Lsn} {record.Operation}" : null);

    private sealed record McpOptionBinding(string Key,
                                           string Label,
                                           string Group,
                                           Func<QueryViewModel, bool> Get,
                                           Action<QueryViewModel, bool> Set,
                                           Func<QueryViewModel, McpOptionUnavailable>? Unavailable = null);
}
