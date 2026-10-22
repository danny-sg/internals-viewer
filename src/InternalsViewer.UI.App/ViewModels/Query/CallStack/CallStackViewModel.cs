using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public partial class CallStackViewModel(QueryViewModel query) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<PlayheadThreadRow> _threads = [];

    [ObservableProperty]
    private CallSignaturePart? _selectedValue;

    [ObservableProperty]
    private PlayheadCallRow? _selectedCall;

    [ObservableProperty]
    private IReadOnlyList<SourceStep> _source = [];

    public virtual string EmptyText => "No Calls At This Position";

    public string Title => Opened is { } opened ? $"Call Stack {opened.Node.Symbol} [{opened.CallIndex + 1:N0}]" : "Call Stack";

    protected QueryViewModel Query { get; } = query;

    protected virtual string CurrentText => "Running At This Position";

    protected virtual bool ShowsMemory => false;

    private IReadOnlyList<TraceThreadStack>? Pending { get; set; }

    private bool IsUpdating { get; set; }

    private PlayheadCallRow? HighlightedRow { get; set; }

    private IReadOnlyList<TraceThreadStack>? Received { get; set; }

    private (CallStackNode Node, int CallIndex)? Opened { get; set; }

    private CallReference? SelectedFrame { get; set; }

    public bool IsShowing(CallStackNode node, int callIndex)
        => Opened is { } opened && ReferenceEquals(opened.Node, node) && opened.CallIndex == callIndex;

    public void SetStack(IReadOnlyList<TraceThreadStack> stacks, CallReference? selected = null)
    {
        var isNewFrame = selected != SelectedFrame;

        SelectedFrame = selected;

        if (Received is { } received && IsSame(received, stacks))
        {
            if (isNewFrame)
            {
                SelectFrame();
            }

            return;
        }

        Received = stacks;

        Pending = stacks;

        if (!IsUpdating)
        {
            _ = UpdateAsync();
        }
    }

    public async Task OpenAsync(CallStackNode node, int callIndex)
    {
        Opened = (node, callIndex);

        TraceTimeline?[] timelines = [Query.TraceTimeline, Query.FullTraceTimeline];

        var stacks = await Task.Run(() => GetStackAt(timelines, node, callIndex));

        SetStack(stacks);
    }

    public void Select(PlayheadCallRow row)
    {
        if (!ReferenceEquals(SelectedCall, row))
        {
            SelectedValue = null;
        }

        SelectedCall = row;

        if (row.CallIndex >= 0)
        {
            ShowCall(row);
        }
    }

    public void ClearSelection()
    {
        SelectedValue = null;

        SelectedCall = null;
    }

    public void OpenInCallTree(PlayheadCallRow row) => Query.OpenCallTree(row.Node, row.CallIndex);

    public virtual void Zoom(PlayheadCallRow row) => UseChart(c => c.ZoomToCall(row.Node, row.CallIndex));

    public void Scope(PlayheadCallRow row) => Scope(row.Node);

    public void Scope(CallStackNode node) => Query.PlayheadStack.RequestChart(c => c.ScopeTo(node));

    public void Mark(SourceStep? step) => Query.PlayheadStack.Chart?.MarkCall(step?.Node, step?.CallIndex ?? -1);

    public void MarkFrame(SourceFrame frame) => Query.PlayheadStack.Chart?.MarkCall(frame.Node, GetEnclosingCall(frame));

    public void GoTo(SourceStep step) => UseChart(c => c.MovePlayheadTo(step.Node, step.CallIndex));

    public void GoTo(SourceFrame frame) => UseChart(c => c.MovePlayheadTo(frame.Node, GetEnclosingCall(frame)));

    public void FindReferences(ulong value)
    {
        Query.Layout.ShowCallTree();

        Query.CallTreeTab.FindReferences(value);
    }

    public void ShowMemory(CallSignaturePart part)
    {
        if (SelectedCall is { } call)
        {
            ShowMemory(call, part);
        }
    }

    public void ShowMemory(PlayheadCallRow row, CallSignaturePart part)
    {
        if (part is { IsMemoryTracked: true, Value: { } address })
        {
            Query.PlayheadMemory.Open(part.MemoryBlock, address, row.FromPosition);
        }
    }

    protected virtual void UseChart(Action<IPlayheadChart> action) => Query.PlayheadStack.RequestChart(action);

    protected virtual void ShowCall(PlayheadCallRow row) => _ = Query.Arguments.ShowAsync(row.Node, row.CallIndex);

    partial void OnSelectedValueChanged(CallSignaturePart? value)
    {
        Source = GetSource(value);

        Highlight();
    }

    partial void OnSelectedCallChanged(PlayheadCallRow? value) => Highlight();

    partial void OnSourceChanged(IReadOnlyList<SourceStep> value) => Mark(null);

    private static IReadOnlyList<TraceThreadStack> GetStackAt(IEnumerable<TraceTimeline?> timelines, CallStackNode node, int callIndex)
    {
        foreach (var timeline in timelines.OfType<TraceTimeline>().Distinct())
        {
            if (timeline.FindCall(node, callIndex) is { } location)
            {
                return timeline.GetStack(location.From.Position);
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

    private void SelectFrame()
    {
        if (SelectedFrame is not { } frame)
        {
            return;
        }

        var row = Threads.SelectMany(t => t.Calls).FirstOrDefault(r => ReferenceEquals(r.Node, frame.Node) && r.CallIndex == frame.CallIndex);

        if (row is null || ReferenceEquals(row, SelectedCall))
        {
            return;
        }

        SelectedValue = null;

        SelectedCall = row;
    }

    private int GetEnclosingCall(SourceFrame frame)
        => Query.Arguments.Lookup?.GetEnclosingCall(frame.Node, frame.Position, frame.Thread) ?? -1;

    private PlayheadCallRow? FindRow(PlayheadCallRow selected)
        => Threads.SelectMany(t => t.Calls)
                  .FirstOrDefault(r => ReferenceEquals(r, selected) || (ReferenceEquals(r.Node, selected.Node) && r.CallIndex == selected.CallIndex));

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

        return SourcePath.Build(value, call, [.. calls.SkipWhile(c => c != call).Skip(1)], origin?.Node, origin?.CallIndex ?? -1);
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

                SelectFrame();

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
        var timeline = Query.TraceTimeline;

        var origin = timeline?.Start.Position.Sequence ?? 0;

        var threads = new List<PlayheadThreadRow>();

        foreach (var stack in stacks)
        {
            var found = new List<(TraceSpan Call, CallStackNode Node)>();

            foreach (var call in stack.Calls)
            {
                if (timeline?.NodeOf(call.CallNodeIndex) is { } node)
                {
                    found.Add((call, node));
                }
            }

            var rows = new List<PlayheadCallRow>();

            for (var index = found.Count - 1; index >= 0; index--)
            {
                rows.Add(await BuildRowAsync(found[index].Node, found[index].Call, stack.ThreadId, index == found.Count - 1, origin));
            }

            if (rows.Count > 0)
            {
                threads.Add(new PlayheadThreadRow(stack.ThreadId, rows));
            }
        }

        return threads;
    }

    private void SetMemoryBlocks(CallSignature signature, TracePosition position)
    {
        foreach (var part in signature.Parts.Append(signature.This))
        {
            if (part is { Kind: CallSignaturePartKind.Value, Value: { } value and not 0 })
            {
                part.MemoryBlock = Query.MemoryHistory.GetBlock(value, position);
            }
        }
    }

    private async Task<PlayheadCallRow> BuildRowAsync(CallStackNode node, TraceSpan call, uint thread, bool isCurrent, ulong origin)
    {
        var index = call.CallIndex;

        var lookup = index >= 0 ? Query.Arguments.Lookup : null;

        var signature = lookup is null ? null : await lookup.GetSignatureAsync(node, index);

        signature ??= new CallSignature([new CallSignaturePart(node.Symbol, CallSignaturePartKind.Function)], null, null);

        var (from, to) = lookup?.GetPositions(node, index) ?? (TracePosition.Unknown, TracePosition.Unknown);

        if (ShowsMemory && from.IsKnown)
        {
            SetMemoryBlocks(signature, from);
        }

        return new PlayheadCallRow(node, call, signature, from, to, thread, isCurrent, origin)
        {
            CurrentText = CurrentText
        };
    }
}
