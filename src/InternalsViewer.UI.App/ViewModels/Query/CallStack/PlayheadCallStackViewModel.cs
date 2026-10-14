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

    public event Action<double, double>? ZoomRequested;

    public event Action<CallStackNode>? ScopeRequested;

    public event Action<bool>? StepRequested;

    public event Action<CallStackNode?, int>? MarkRequested;

    public event Action<CallStackNode, int>? GoToRequested;

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

    public void OpenInCallTree(PlayheadCallRow row) => query.OpenCallTree(row.Node, row.Call);

    public void Zoom(PlayheadCallRow row) => ZoomRequested?.Invoke(row.Start, row.End);

    public void Scope(PlayheadCallRow row) => ScopeRequested?.Invoke(row.Node);

    public void Step(bool forward)
    {
        ClearSelection();

        StepRequested?.Invoke(forward);
    }

    public void ClearSelection()
    {
        SelectedValue = null;

        SelectedCall = null;
    }

    public void Mark(SourceStep? step) => MarkRequested?.Invoke(step?.Node, step?.Call ?? -1);

    public void MarkFrame(SourceFrame frame)
        => MarkRequested?.Invoke(frame.Node, query.Arguments.Lookup?.GetEnclosingCall(frame.Node, frame.Moment) ?? -1);

    public void GoTo(SourceStep step) => GoToRequested?.Invoke(step.Node, step.Call);

    public void Find(ulong value)
    {
        query.Layout.ShowCallTree();

        query.CallTreeTab.Find(value);
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

    private PlayheadCallRow? FindRow(PlayheadCallRow selected)
    {
        foreach (var thread in Threads)
        {
            foreach (var row in thread.Calls)
            {
                if (ReferenceEquals(row, selected) || (ReferenceEquals(row.Node, selected.Node) && row.Call == selected.Call))
                {
                    return row;
                }
            }
        }

        return null;
    }

    private static bool IsSame(IReadOnlyList<TraceThreadStack> current, IReadOnlyList<TraceThreadStack> next)
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (current[index].ThreadId != next[index].ThreadId || !current[index].Calls.SequenceEqual(next[index].Calls))
            {
                return false;
            }
        }

        return true;
    }

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
        var lookup = query.Arguments.Lookup;

        var timeline = query.FlameChart;

        var origin = timeline?.StartOf(TraceTimelineAxis.Position) ?? 0;

        var threads = new List<PlayheadThreadRow>();

        foreach (var stack in stacks)
        {
            var found = new List<(CallStackNode Node, TraceTimelineSpan Span, TraceCallExtent Extent, CallSignature Signature, ulong Id)>();

            for (var index = 0; index < stack.Calls.Count; index++)
            {
                var span = stack.Calls[index];

                if (timeline?.NodeOf(span.Node) is not { } node)
                {
                    continue;
                }

                var signature = lookup is not null && span.Call >= 0 ? await lookup.GetSignatureAsync(node, span.Call) : null;

                signature ??= new CallSignature([new CallSignaturePart(node.Symbol, CallSignaturePartKind.Function)], null, null);

                var id = lookup is not null && span.Call >= 0 ? lookup.GetCallId(node, span.Call) : 0;

                found.Add((node, span, stack.Extents[index], signature, id));
            }

            var rows = found.Select((f, depth) => new PlayheadCallRow(f.Node,
                                                                      f.Span,
                                                                      f.Signature,
                                                                      f.Id,
                                                                      depth == found.Count - 1,
                                                                      f.Extent,
                                                                      origin));

            List<PlayheadCallRow> calls = [.. rows.Reverse()];

            if (calls.Count > 0)
            {
                threads.Add(new PlayheadThreadRow(stack.ThreadId, calls));
            }
        }

        return threads;
    }
}
