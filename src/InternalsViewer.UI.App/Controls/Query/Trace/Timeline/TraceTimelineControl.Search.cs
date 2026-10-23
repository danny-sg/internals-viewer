using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const byte SearchDimAlpha = 110;

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText),
                                    typeof(string),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnSearchTextChanged));

    public string? SearchText
    {
        get => (string?)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly DependencyProperty HasSearchMatchesProperty =
        DependencyProperty.Register(nameof(HasSearchMatches), typeof(bool), typeof(TraceTimelineControl), new PropertyMetadata(false));

    public bool HasSearchMatches
    {
        get => (bool)GetValue(HasSearchMatchesProperty);
        private set => SetValue(HasSearchMatchesProperty, value);
    }

    public static readonly DependencyProperty SearchMatchCountProperty =
        DependencyProperty.Register(nameof(SearchMatchCount), typeof(int?), typeof(TraceTimelineControl), new PropertyMetadata(null));

    public int? SearchMatchCount
    {
        get => (int?)GetValue(SearchMatchCountProperty);
        private set => SetValue(SearchMatchCountProperty, value);
    }

    private bool[]? _searchMatches;

    private bool[]? _nodeMatches;

    private bool[]? _memoryScope;

    private CallStackNode? _memoryScopeNode;

    private TraceTimeline? _memoryScopeTimeline;

    private bool[]? _memoryWriters;

    private int? _memoryWritersBlock;

    private bool[]? _highlightedCalls;

    public void StepSearch(bool forward)
    {
        if (GetAdjacentSearchMatch(forward) is not { } hit || SpanOf(hit) is null)
        {
            return;
        }

        SelectAndShow(hit);
    }

    public void HighlightCalls(IReadOnlyCollection<CallStackNode>? nodes)
    {
        _highlightedCalls = nodes is { Count: > 0 } && _timeline is { } timeline ? CallsOf(timeline, nodes) : null;

        _colours.Clear();

        Redraw();
    }

    private void FindSearchMatches()
    {
        _colours.Clear();

        _searchMatches = null;

        var terms = SearchText?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (terms.Length > 0 && _timeline is { } timeline)
        {
            var matches = new bool[timeline.NodeCount];

            for (var callNodeIndex = 0; callNodeIndex < matches.Length; callNodeIndex++)
            {
                var label = LabelOf(callNodeIndex);

                matches[callNodeIndex] = terms.All(t => label.Contains(t, StringComparison.OrdinalIgnoreCase));
            }

            _searchMatches = matches;
        }

        CountSearchMatches();
    }

    private void CountSearchMatches()
    {
        if (_searchMatches is not { } matches || _visible is not { } timeline)
        {
            SearchMatchCount = _searchMatches is null ? null : 0;

            HasSearchMatches = false;

            return;
        }

        var count = 0;

        foreach (var thread in timeline.Threads)
        {
            foreach (var row in thread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    if (IsSearchMatch(matches, row.GetCallNodeIndex(index)))
                    {
                        count++;
                    }
                }
            }
        }

        SearchMatchCount = count;

        HasSearchMatches = count > 0;
    }

    private FlameHit? GetAdjacentSearchMatch(bool forward)
    {
        if (_searchMatches is not { } matches || _visible is not { } timeline)
        {
            return null;
        }

        var current = _selected is { } selected && SpanOf(selected) is { } span
            ? new SearchPosition(TraceScale.GetValue(span.From, _axis), selected.Lane, selected.Depth)
            : new SearchPosition(_playhead ?? _viewStart, forward ? int.MaxValue : -1, 0);

        FlameHit? best = null;

        var bestStart = 0d;

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var rows = timeline.Threads[lane].Rows;

            for (var depth = 0; depth < rows.Count; depth++)
            {
                var row = rows[depth];

                var order = (lane, depth).CompareTo((current.Lane, current.Depth));

                if (forward)
                {
                    var first = order > 0
                        ? row.GetFirstStartingFrom(_axis, current.Start, 0)
                        : row.GetFirstStartingAfter(_axis, current.Start);

                    for (var index = first; index < row.Count && (best is null || row.GetStart(_axis, index) < bestStart); index++)
                    {
                        if (IsSearchMatch(matches, row.GetCallNodeIndex(index)))
                        {
                            best = new FlameHit(lane, depth, index);

                            bestStart = row.GetStart(_axis, index);

                            break;
                        }
                    }
                }
                else
                {
                    var last = (order < 0
                        ? row.GetFirstStartingAfter(_axis, current.Start)
                        : row.GetFirstStartingFrom(_axis, current.Start, 0)) - 1;

                    for (var index = last; index >= 0 && (best is null || row.GetStart(_axis, index) >= bestStart); index--)
                    {
                        if (IsSearchMatch(matches, row.GetCallNodeIndex(index)))
                        {
                            best = new FlameHit(lane, depth, index);

                            bestStart = row.GetStart(_axis, index);

                            break;
                        }
                    }
                }
            }
        }

        return best;
    }

    private static bool IsSearchMatch(bool[] matches, int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < matches.Length && matches[callNodeIndex];

    private void FindNodeMatches(CallStackNode? selected)
    {
        _colours.Clear();

        _nodeMatches = null;

        if (selected is null || _timeline is not { } timeline)
        {
            return;
        }

        var members = GetMembers(selected);

        _nodeMatches = CallsUnder(timeline, members.Contains)
                       ?? (selected.Frame is { } frame ? CallsUnder(timeline, n => ReferenceEquals(n.Frame, frame)) : null);
    }

    private bool[]? GetMemoryScope()
    {
        var node = _selected is { } selected && SpanOf(selected) is { } span ? _visible?.NodeOf(span.CallNodeIndex) : null;

        if (ReferenceEquals(node, _memoryScopeNode) && ReferenceEquals(_timeline, _memoryScopeTimeline))
        {
            return _memoryScope;
        }

        _memoryScopeNode = node;

        _memoryScopeTimeline = _timeline;

        _memoryScope = node is not null && _timeline is { } timeline ? CallsUnder(timeline, GetMembers(node).Contains) : null;

        return _memoryScope;
    }

    private static HashSet<CallStackNode> GetMembers(CallStackNode node) => [.. node.MergedFrom.Count > 0 ? node.MergedFrom : [node]];

    private static bool[]? CallsUnder(TraceTimeline timeline, Func<CallStackNode, bool> isSelected)
    {
        var under = new bool[timeline.NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < under.Length; callNodeIndex++)
        {
            var parent = timeline.ParentOf(callNodeIndex);

            under[callNodeIndex] = (parent >= 0 && under[parent]) || (timeline.NodeOf(callNodeIndex) is { } callNode && isSelected(callNode));

            any |= under[callNodeIndex];
        }

        return any ? under : null;
    }

    private static bool[]? CallsOf(TraceTimeline timeline, IReadOnlyCollection<CallStackNode> nodes)
    {
        var wanted = new HashSet<CallStackNode>(nodes, ReferenceEqualityComparer.Instance);

        var calls = new bool[timeline.NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < calls.Length; callNodeIndex++)
        {
            calls[callNodeIndex] = timeline.NodeOf(callNodeIndex) is { } callNode && wanted.Contains(callNode);

            any |= calls[callNodeIndex];
        }

        return any ? calls : null;
    }

    private bool IsDimmed(int callNodeIndex)
        => _highlightedCalls is { } highlighted
            ? Excludes(highlighted, callNodeIndex)
            : _memoryWriters is { } writers ? Excludes(writers, callNodeIndex) : Excludes(_searchMatches, callNodeIndex);

    private void SetMemoryWriters(int? block)
    {
        if (block == _memoryWritersBlock)
        {
            return;
        }

        _memoryWritersBlock = block;

        _memoryWriters = block is { } index && _timeline is { } timeline ? _memory.GetWriters(index, timeline.NodeCount) : null;

        _colours.Clear();

        Redraw();
    }

    private static bool Excludes(bool[]? matches, int callNodeIndex) => matches is not null && callNodeIndex >= 0 && callNodeIndex < matches.Length && !matches[callNodeIndex];

    private static SKColor Dimmed(SKColor colour)
    {
        var grey = (byte)(colour.Red * 0.299 + colour.Green * 0.587 + colour.Blue * 0.114);

        return new SKColor(grey, grey, grey, SearchDimAlpha);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.FindSearchMatches();

        control.Redraw();
    }

    private readonly record struct SearchPosition(double Start, int Lane, int Depth);
}
