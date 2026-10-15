using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public partial class CallStackViewModel(QueryViewModel query) : ObservableObject
{
    private const string RowBufferTitle = "Row Buffer";

    [ObservableProperty]
    private IReadOnlyList<PlayheadThreadRow> _threads = [];

    [ObservableProperty]
    private CallSignaturePart? _selectedValue;

    [ObservableProperty]
    private PlayheadCallRow? _selectedCall;

    [ObservableProperty]
    private PlayheadCallRow? _rowBufferCall;

    [ObservableProperty]
    private IReadOnlyList<SourceStep> _source = [];

    public virtual string EmptyText => "No Calls At This Position";

    public string Title => Opened is { } opened ? $"Call Stack {opened.Node.Symbol} [{opened.Call + 1:N0}]" : "Call Stack";

    protected QueryViewModel Query { get; } = query;

    protected virtual string CurrentText => "Running At This Position";

    private IReadOnlyList<TraceThreadStack>? Pending { get; set; }

    private bool IsUpdating { get; set; }

    private PlayheadCallRow? HighlightedRow { get; set; }

    private IReadOnlyList<TraceThreadStack>? Received { get; set; }

    private (CallStackNode Node, int Call)? Opened { get; set; }

    public bool IsShowing(CallStackNode node, int call)
        => Opened is { } opened && ReferenceEquals(opened.Node, node) && opened.Call == call;

    public void SetStack(IReadOnlyList<TraceThreadStack> stacks)
    {
        if (Received is { } received && IsSame(received, stacks))
        {
            return;
        }

        Received = stacks;

        Pending = stacks;

        if (!IsUpdating)
        {
            _ = UpdateAsync();
        }
    }

    public async Task OpenAsync(CallStackNode node, int call)
    {
        Opened = (node, call);

        TraceTimeline?[] timelines = [Query.FlameChart, Query.FullFlameChart];

        var stacks = await Task.Run(() => GetStackAt(timelines, node, call));

        SetStack(stacks);
    }

    public void Select(PlayheadCallRow row)
    {
        if (!ReferenceEquals(SelectedCall, row))
        {
            SelectedValue = null;
        }

        RowBufferCall = null;

        SelectedCall = row;

        if (row.Call >= 0)
        {
            ShowCall(row);
        }
    }

    public void ClearSelection()
    {
        SelectedValue = null;

        SelectedCall = null;

        RowBufferCall = null;
    }

    public void ShowRowBuffer(PlayheadCallRow row)
    {
        RowBufferCall = row;

        SelectedValue = null;

        SelectedCall = row;
    }

    public ResultDetails GetRowBufferDetails(PlayheadCallRow row, bool decode)
    {
        var lookup = Query.Arguments.Lookup;

        var rowBuffer = lookup is not null && row.Node.Frame is { } frame && row.Call >= 0
            ? lookup.RowBuffers.GetRow(frame.Address, frame.Instance, row.Call)
            : null;

        return RowBufferDisplay.GetDetails(RowBufferTitle, rowBuffer, lookup?.RowBufferDecoder, decode);
    }

    public Task OpenPagePointerAsync(RowBufferPage pointer, bool detached) => Query.CallTreeTab.OpenPagePointerAsync(pointer, detached);

    public void OpenInCallTree(PlayheadCallRow row) => Query.OpenCallTree(row.Node, row.Call);

    public virtual void Zoom(PlayheadCallRow row) => UseChart(c => c.ZoomToCall(row.Node, row.Call));

    public void Scope(PlayheadCallRow row) => Scope(row.Node);

    public void Scope(CallStackNode node) => Query.PlayheadStack.RequestChart(c => c.ScopeTo(node));

    public void Mark(SourceStep? step) => Query.PlayheadStack.Chart?.MarkCall(step?.Node, step?.Call ?? -1);

    public void MarkFrame(SourceFrame frame) => Query.PlayheadStack.Chart?.MarkCall(frame.Node, GetEnclosingCall(frame));

    public void GoTo(SourceStep step) => UseChart(c => c.MovePlayheadTo(step.Node, step.Call));

    public void GoTo(SourceFrame frame) => UseChart(c => c.MovePlayheadTo(frame.Node, GetEnclosingCall(frame)));

    public void FindReferences(ulong value)
    {
        Query.Layout.ShowCallTree();

        Query.CallTreeTab.FindReferences(value);
    }

    protected virtual void UseChart(Action<IPlayheadChart> action) => Query.PlayheadStack.RequestChart(action);

    protected virtual void ShowCall(PlayheadCallRow row) => _ = Query.Arguments.ShowAsync(row.Node, row.Call);

    partial void OnSelectedValueChanged(CallSignaturePart? value)
    {
        if (value is not null)
        {
            RowBufferCall = null;
        }

        Source = GetSource(value);

        Highlight();
    }

    partial void OnSelectedCallChanged(PlayheadCallRow? value) => Highlight();

    partial void OnSourceChanged(IReadOnlyList<SourceStep> value) => Mark(null);

    private static IReadOnlyList<TraceThreadStack> GetStackAt(IEnumerable<TraceTimeline?> timelines, CallStackNode node, int call)
    {
        foreach (var timeline in timelines.OfType<TraceTimeline>().Distinct())
        {
            foreach (var location in timeline.GetSpanLocations())
            {
                if (location.Call == call && ReferenceEquals(location.Node, node))
                {
                    return timeline.StackAt(TraceTimelineAxis.Position, location.StartOf(TraceTimelineAxis.Position));
                }
            }
        }

        return [];
    }

    private void Highlight()
    {
        if (HighlightedRow is { } previous)
        {
            previous.Highlighted = null;
        }

        HighlightedRow = SelectedCall is { } selected ? FindRow(selected) : null;

        if (HighlightedRow is not { } row || SelectedCall is not { } call)
        {
            return;
        }

        var index = SelectedValue is { } value ? IndexOf(call.Signature, value) : -1;

        row.Highlighted = index >= 0 && index < row.Signature.Count
            ? row.Signature[index]
            : row.Signature.FirstOrDefault(p => p.Kind == CallSignaturePartKind.Function);
    }

    private int GetEnclosingCall(SourceFrame frame) => Query.Arguments.Lookup?.GetEnclosingCall(frame.Node, frame.Moment) ?? -1;

    private PlayheadCallRow? FindRow(PlayheadCallRow selected)
        => Threads.SelectMany(t => t.Calls)
                  .FirstOrDefault(r => ReferenceEquals(r, selected) || (ReferenceEquals(r.Node, selected.Node) && r.Call == selected.Call));

    private static bool IsSame(IReadOnlyList<TraceThreadStack> current, IReadOnlyList<TraceThreadStack> next)
        => current.Count == next.Count
           && current.Zip(next).All(p => p.First.ThreadId == p.Second.ThreadId && p.First.Calls.SequenceEqual(p.Second.Calls));

    private static int IndexOf(IReadOnlyList<CallSignaturePart> parts, CallSignaturePart part)
    {
        for (var index = 0; index < parts.Count; index++)
        {
            if (ReferenceEquals(parts[index], part))
            {
                return index;
            }
        }

        return -1;
    }

    private IReadOnlyList<SourceStep> GetSource(CallSignaturePart? value)
    {
        if (value is not { CanFind: true } || SelectedCall is not { } call)
        {
            return [];
        }

        var calls = Threads.FirstOrDefault(t => t.Calls.Contains(call))?.Calls ?? [];

        var origin = value.Origin is { } entry ? Query.Arguments.Lookup?.GetOrigin(entry) : null;

        return SourcePath.Build(value, call, [.. calls.SkipWhile(c => c != call).Skip(1)], origin?.Node, origin?.Call ?? -1);
    }

    private async Task UpdateAsync()
    {
        IsUpdating = true;

        try
        {
            while (Pending is { } stacks)
            {
                Pending = null;

                Threads = await BuildAsync(stacks);

                Highlight();
            }
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async Task<IReadOnlyList<PlayheadThreadRow>> BuildAsync(IReadOnlyList<TraceThreadStack> stacks)
    {
        var timeline = Query.FlameChart;

        var origin = timeline?.StartOf(TraceTimelineAxis.Position) ?? 0;

        var threads = new List<PlayheadThreadRow>();

        foreach (var stack in stacks)
        {
            var found = new List<(TraceStackCall Call, CallStackNode Node)>();

            foreach (var call in stack.Calls)
            {
                if (timeline?.NodeOf(call.Span.Node) is { } node)
                {
                    found.Add((call, node));
                }
            }

            var rows = new List<PlayheadCallRow>();

            for (var index = found.Count - 1; index >= 0; index--)
            {
                rows.Add(await BuildRowAsync(found[index].Node, found[index].Call, index == found.Count - 1, origin));
            }

            if (rows.Count > 0)
            {
                threads.Add(new PlayheadThreadRow(stack.ThreadId, rows));
            }
        }

        return threads;
    }

    private async Task<PlayheadCallRow> BuildRowAsync(CallStackNode node, TraceStackCall call, bool isCurrent, double origin)
    {
        var index = call.Span.Call;

        var lookup = index >= 0 ? Query.Arguments.Lookup : null;

        var signature = lookup is null ? null : await lookup.GetSignatureAsync(node, index);

        signature ??= new CallSignature([new CallSignaturePart(node.Symbol, CallSignaturePartKind.Function)], null, null);

        var hasRowBuffer = lookup is not null && node.Frame is { } frame && lookup.RowBuffers.HasRow(frame.Address, frame.Instance, index);

        return new PlayheadCallRow(node, call, signature, lookup?.GetCallId(node, index) ?? 0, isCurrent, origin)
        {
            HasRowBuffer = hasRowBuffer,
            CurrentText = CurrentText
        };
    }
}
