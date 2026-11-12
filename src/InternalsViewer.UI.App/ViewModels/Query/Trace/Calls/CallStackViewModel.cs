using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Calls;

public partial class CallStackViewModel(QueryViewModel query) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<PlayheadThreadRow> _threads = [];

    [ObservableProperty]
    private PlayheadCallRow? _selectedCall;

    public virtual string EmptyText => "No Calls At This Position";

    public string Title => Opened is { } opened ? $"Call Stack {opened.Node.Symbol} [{opened.CallIndex + 1:N0}]" : "Call Stack";

    public ITraceNavigator Navigator => Query.TraceTimelineTab;

    protected QueryViewModel Query { get; } = query;

    protected virtual string CurrentText => "Running At This Position";

    protected virtual bool ShowsMemory => false;

    private IReadOnlyList<TraceThreadStack>? Pending { get; set; }

    private bool IsUpdating { get; set; }

    private PlayheadCallRow? HighlightedRow { get; set; }

    private IReadOnlyList<TraceThreadStack>? Received { get; set; }

    private (CallTreeNode Node, int CallIndex)? Opened { get; set; }

    private CallReference? SelectedFrame { get; set; }

    public bool IsShowing(CallTreeNode node, int callIndex)
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

    public async Task OpenAsync(CallTreeNode node, int callIndex)
    {
        Opened = (node, callIndex);

        TraceTimeline?[] timelines = [Query.TraceTimeline, Query.FullTraceTimeline];

        var stacks = await Task.Run(() => GetStackAt(timelines, node, callIndex));

        SetStack(stacks);
    }

    public void Select(PlayheadCallRow row)
    {
        SelectedCall = row;

        if (row.CallIndex >= 0)
        {
            ShowCall(row);
        }
    }

    public void ClearSelection() => SelectedCall = null;

    public void Clear()
    {
        ClearSelection();

        SetStack([]);
    }

    public void OpenInCallTree(PlayheadCallRow row) => Query.OpenCallTree(row.Node, row.CallIndex);

    public virtual void Zoom(PlayheadCallRow row)
        => Query.TraceTimelineTab.PlayheadStack.RequestChart(c => c.ZoomToCall(row.Node, row.CallIndex));

    public void Scope(PlayheadCallRow row) => Scope(row.Node);

    public void Scope(CallTreeNode node) => Query.TraceTimelineTab.PlayheadStack.RequestChart(c => c.ScopeTo(node));

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
            var position = ReferenceEquals(part, row.Return) ? row.ToPosition : row.FromPosition;

            Query.TraceTimelineTab.OpenMemory(part.MemoryBlock, address, position);
        }
        else if (part.BufferPage is { } page)
        {
            Navigator.OpenBufferPage(page);
        }
    }

    protected virtual void ShowCall(PlayheadCallRow row) => _ = Query.Arguments.ShowAsync(row.Node, row.CallIndex);

    partial void OnSelectedCallChanged(PlayheadCallRow? value) => Highlight();

    private static IReadOnlyList<TraceThreadStack> GetStackAt(IEnumerable<TraceTimeline?> timelines, CallTreeNode node, int callIndex)
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

        if (HighlightedRow is { } row)
        {
            row.Highlighted = row.Signature.FirstOrDefault(p => p.Kind == CallSignaturePartKind.Function);
        }
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

        SelectedCall = row;
    }

    private PlayheadCallRow? FindRow(PlayheadCallRow selected)
        => Threads.SelectMany(t => t.Calls)
                  .FirstOrDefault(r => ReferenceEquals(r, selected) || (ReferenceEquals(r.Node, selected.Node) && r.CallIndex == selected.CallIndex));

    private static bool IsSame(IReadOnlyList<TraceThreadStack> current, IReadOnlyList<TraceThreadStack> next)
        => current.Count == next.Count
           && current.Zip(next).All(p => p.First.ThreadId == p.Second.ThreadId && p.First.Calls.SequenceEqual(p.Second.Calls));

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
            var found = new List<(TraceSpan Call, CallTreeNode Node)>();

            foreach (var call in stack.Calls)
            {
                if (timeline?.GetNode(call.CallNodeIndex) is { } node)
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

    private void SetMemoryBlocks(CallSignature signature, TracePosition from, TracePosition to)
    {
        foreach (var part in signature.Parts.Append(signature.This))
        {
            if (!ReferenceEquals(part, signature.Return))
            {
                SetMemoryBlock(part, from);
            }
        }

        if (to.IsKnown)
        {
            SetMemoryBlock(signature.Return, to);
        }
    }

    private void SetMemoryBlock(CallSignaturePart? part, TracePosition position)
    {
        if (part is not { Kind: CallSignaturePartKind.Value, Value: { } value and not 0 })
        {
            return;
        }

        var history = Query.MemoryHistory;

        part.MemoryBlock = history.GetBlock(value, position);

        part.BufferPage = part is { IsMemoryTracked: false, DecodedString: null } ? history.GetBufferPage(value, position) : null;

        part.CanOpenTarget = part.BufferPage is not { } page || history.IsQueryDatabase(page);

        part.MemoryTarget = part switch
        {
            { IsMemoryTracked: true, DecodedString: null } => history.GetPointerTarget(value, position),
            { BufferPage: { } buffer } => history.GetBufferPageLabel(buffer),
            _ => null
        };
    }

    private string? GetObjectClass(CallSignaturePart? self, TracePosition position)
        => self is { Value: { } value and not 0 } && Query.MemoryHistory.AddressMap.GetEntry(value, position) is { } entry
            ? entry.VtableClassName ?? entry.ClassName
            : null;

    private async Task<PlayheadCallRow> BuildRowAsync(CallTreeNode node, TraceSpan call, bool isCurrent, ulong origin)
    {
        var index = call.CallIndex;

        var lookup = index >= 0 ? Query.Arguments.Lookup : null;

        var signature = lookup is null ? null : await lookup.GetSignatureAsync(node, index);

        signature ??= new CallSignature([new CallSignaturePart(node.Symbol, CallSignaturePartKind.Function)], null, null);

        var (from, to) = lookup?.GetPositions(node, index) ?? (TracePosition.Unknown, TracePosition.Unknown);

        if (ShowsMemory && from.IsKnown)
        {
            SetMemoryBlocks(signature, from, to);
        }

        return new PlayheadCallRow(node, call, signature, from, to, isCurrent, origin, GetObjectClass(signature.This, from))
        {
            CurrentText = CurrentText
        };
    }
}
