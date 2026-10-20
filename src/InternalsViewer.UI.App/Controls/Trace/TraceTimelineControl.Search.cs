using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

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

    public void StepSearch(bool forward)
    {
        if (GetAdjacentSearchMatch(forward) is not { } hit || SpanOf(hit) is not { } span)
        {
            return;
        }

        _playhead = span.Start;

        SelectAndShow(hit);
    }

    private void FindSearchMatches()
    {
        _colours.Clear();

        _searchMatches = null;

        var terms = SearchText?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (terms.Length > 0 && _timeline is { } timeline)
        {
            var matches = new bool[timeline.NodeCount];

            for (var node = 0; node < matches.Length; node++)
            {
                var label = LabelOf(node);

                matches[node] = terms.All(t => label.Contains(t, StringComparison.OrdinalIgnoreCase));
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
                    if (IsSearchMatch(matches, row.NodeAt(index)))
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
            ? new SearchPosition(span.Start, selected.Lane, selected.Depth)
            : new SearchPosition(_playhead ?? _viewStart, forward ? int.MaxValue : -1, 0);

        FlameHit? best = null;

        var bestStart = 0d;

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var rows = timeline.Threads[lane].Rows;

            for (var depth = 0; depth < rows.Count; depth++)
            {
                var row = rows[depth];

                var starts = row.Starts(_axis);

                var order = (lane, depth).CompareTo((current.Lane, current.Depth));

                if (forward)
                {
                    var first = order > 0 ? row.FirstStartingFrom(_axis, current.Start, 0) : row.FirstStartingAfter(_axis, current.Start);

                    for (var index = first; index < row.Count && (best is null || starts[index] < bestStart); index++)
                    {
                        if (IsSearchMatch(matches, row.NodeAt(index)))
                        {
                            best = new FlameHit(lane, depth, index);

                            bestStart = starts[index];

                            break;
                        }
                    }
                }
                else
                {
                    var last = (order < 0 ? row.FirstStartingAfter(_axis, current.Start) : row.FirstStartingFrom(_axis, current.Start, 0)) - 1;

                    for (var index = last; index >= 0 && (best is null || starts[index] >= bestStart); index--)
                    {
                        if (IsSearchMatch(matches, row.NodeAt(index)))
                        {
                            best = new FlameHit(lane, depth, index);

                            bestStart = starts[index];

                            break;
                        }
                    }
                }
            }
        }

        return best;
    }

    private static bool IsSearchMatch(bool[] matches, int node) => node >= 0 && node < matches.Length && matches[node];

    private void FindNodeMatches(CallStackNode? selected)
    {
        _colours.Clear();

        _nodeMatches = null;

        if (selected is null || _timeline is not { } timeline)
        {
            return;
        }

        HashSet<CallStackNode> members = [.. selected.MergedFrom.Count > 0 ? selected.MergedFrom : [selected]];

        _nodeMatches = CallsUnder(timeline, members.Contains)
                       ?? (selected.Frame is { } frame ? CallsUnder(timeline, n => ReferenceEquals(n.Frame, frame)) : null);
    }

    private static bool[]? CallsUnder(TraceTimeline timeline, Func<CallStackNode, bool> isSelected)
    {
        var under = new bool[timeline.NodeCount];

        var any = false;

        for (var node = 0; node < under.Length; node++)
        {
            var parent = timeline.ParentOf(node);

            under[node] = (parent >= 0 && under[parent]) || (timeline.NodeOf(node) is { } callNode && isSelected(callNode));

            any |= under[node];
        }

        return any ? under : null;
    }

    private bool IsDimmed(int node) => Excludes(_searchMatches, node);

    private static bool Excludes(bool[]? matches, int node) => matches is not null && node >= 0 && node < matches.Length && !matches[node];

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
