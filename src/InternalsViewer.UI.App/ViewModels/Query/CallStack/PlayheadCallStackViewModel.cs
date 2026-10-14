using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class PlayheadCallStackViewModel(QueryViewModel query) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<PlayheadThreadRow> _threads = [];

    [ObservableProperty]
    private CallSignaturePart? _selectedValue;

    [ObservableProperty]
    private PlayheadCallRow? _selectedCall;

    [ObservableProperty]
    private IReadOnlyList<SourceStep> _source = [];

    public IPlayheadChart? Chart
    {
        get;
        set
        {
            field = value;

            if (value is not null && ChartRequest is { } request)
            {
                ChartRequest = null;

                request(value);
            }
        }
    }

    private Action<IPlayheadChart>? ChartRequest { get; set; }

    private IReadOnlyList<TraceThreadStack>? Pending { get; set; }

    private bool IsUpdating { get; set; }

    private PlayheadCallRow? HighlightedRow { get; set; }

    private IReadOnlyList<TraceThreadStack>? Received { get; set; }

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

    public void Select(PlayheadCallRow row)
    {
        if (!ReferenceEquals(SelectedCall, row))
        {
            SelectedValue = null;
        }

        SelectedCall = row;

        if (row.Call >= 0)
        {
            query.NavigateToCall(row.Node, row.Call);
        }
    }

    public void ClearSelection()
    {
        SelectedValue = null;

        SelectedCall = null;
    }

    public void OpenInCallTree(PlayheadCallRow row) => query.OpenCallTree(row.Node, row.Call);

    public void Zoom(PlayheadCallRow row) => Chart?.ZoomTo(row.Start, row.End);

    public void Scope(PlayheadCallRow row) => Scope(row.Node);

    public void Scope(CallStackNode node) => RequestChart(c => c.ScopeTo(node));

    public void Open(CallStackNode node, int call) => RequestChart(c => c.OpenCallStack(node, call));

    public void Step(bool forward)
    {
        ClearSelection();

        Chart?.StepPlayhead(forward);
    }

    public void Mark(SourceStep? step) => Chart?.MarkCall(step?.Node, step?.Call ?? -1);

    public void MarkFrame(SourceFrame frame)
        => Chart?.MarkCall(frame.Node, query.Arguments.Lookup?.GetEnclosingCall(frame.Node, frame.Moment) ?? -1);

    public void GoTo(SourceStep step) => Chart?.MovePlayheadTo(step.Node, step.Call);

    public void FindReferences(ulong value)
    {
        query.Layout.ShowCallTree();

        query.CallTreeTab.FindReferences(value);
    }

    partial void OnSelectedValueChanged(CallSignaturePart? value)
    {
        Source = GetSource(value);

        Highlight();
    }

    partial void OnSelectedCallChanged(PlayheadCallRow? value) => Highlight();

    partial void OnSourceChanged(IReadOnlyList<SourceStep> value) => Mark(null);

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

    private void RequestChart(Action<IPlayheadChart> request)
    {
        query.Layout.ShowFlameChart();

        if (Chart is { } chart)
        {
            request(chart);
        }
        else
        {
            ChartRequest = request;
        }
    }

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

        var origin = value.Origin is { } entry ? query.Arguments.Lookup?.GetOrigin(entry) : null;

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
        var timeline = query.FlameChart;

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

        var lookup = index >= 0 ? query.Arguments.Lookup : null;

        var signature = lookup is null ? null : await lookup.GetSignatureAsync(node, index);

        signature ??= new CallSignature([new CallSignaturePart(node.Symbol, CallSignaturePartKind.Function)], null, null);

        return new PlayheadCallRow(node, call, signature, lookup?.GetCallId(node, index) ?? 0, isCurrent, origin);
    }
}
