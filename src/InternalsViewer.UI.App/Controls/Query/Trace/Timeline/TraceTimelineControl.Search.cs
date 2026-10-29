using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const byte SearchDimAlpha = 110;

    private const float SearchMatchThickness = 4;

    private const float MinimumSearchMatchLength = 2;

    private const float SearchMatchHitSlop = 2;

    private const float HalfPixel = 0.5f;

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

    private string[] _searchTerms = [];

    private bool[]? _searchMatches;

    private int[] _memoryMatches = [];

    private (bool[] Matches, TraceTimeline Timeline, TraceTimelineAxis Axis, SearchBars?[] Lanes)? _callMatchBars;

    private (TraceTimelineAxis Axis, double[] Starts)? _memoryMatchStarts;

    private bool[]? _nodeMatches;

    private bool[]? _memoryScope;

    private CallStackNode? _memoryScopeNode;

    private TraceTimeline? _memoryScopeTimeline;

    private bool[]? _memoryWriters;

    private int? _memoryWritersBlock;

    public void StepSearch(bool forward)
    {
        var call = GetAdjacentSearchMatch(forward);

        var callStart = call is { } found && SpanOf(found) is { } span ? TraceScale.GetValue(span.From, _axis) : (double?)null;

        var memoryStart = GetAdjacentMemoryMatch(forward);

        if (memoryStart is { } start && (callStart is not { } other || (forward ? start < other : start > other)))
        {
            RememberLocation();

            StopPlay();

            PlacePlayhead(start);

            return;
        }

        if (callStart is null || call is not { } hit)
        {
            return;
        }

        RememberLocation();

        SelectAndShow(hit);
    }

    private void FindSearchMatches()
    {
        _searchMatches = null;

        _searchTerms = SearchText?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (_searchTerms.Length > 0 && _timeline is { } timeline)
        {
            var matches = new bool[timeline.NodeCount];

            for (var callNodeIndex = 0; callNodeIndex < matches.Length; callNodeIndex++)
            {
                matches[callNodeIndex] = MatchesSearch(LabelOf(callNodeIndex));
            }

            _searchMatches = matches;
        }

        FindMemorySearchMatches();

        CountSearchMatches();
    }

    private void FindMemorySearchMatches()
    {
        _memoryMatchStarts = null;

        var blocks = _memory.Blocks;

        _memoryMatches = _searchTerms.Length == 0 ? [] : [.. Enumerable.Range(0, blocks.Count).Where(i => MatchesSearch(blocks[i].Label))];
    }

    private bool MatchesSearch(string label) => _searchTerms.All(t => label.Contains(t, StringComparison.OrdinalIgnoreCase));

    private void CountSearchMatches()
    {
        if (_searchTerms.Length == 0)
        {
            SearchMatchCount = null;

            HasSearchMatches = false;

            return;
        }

        var count = _memoryMatches.Length;

        if (_searchMatches is { } matches && _visible is { } timeline)
        {
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
        }

        SearchMatchCount = count;

        HasSearchMatches = count > 0;
    }

    private double? GetAdjacentMemoryMatch(bool forward)
    {
        if (GetMemoryMatchStarts() is not { } starts)
        {
            return null;
        }

        var from = GetSelectedStart() ?? _playhead ?? _viewStart;

        var index = forward ? SortedSearch.FirstAfter(starts, from) : SortedSearch.FirstAtOrAfter(starts, from) - 1;

        return index >= 0 && index < starts.Length ? starts[index] : null;
    }

    private void DrawHeaderMarks(SKCanvas canvas, int width, int height)
    {
        var scale = PixelsPerUnit(width);

        var calls = _searchTerms.Length > 0 && ShowsFlameChart ? GetCallMatchBars() : null;

        var updates = ShowsFlameChart ? GetMemoryUpdateMarks() : null;

        if ((calls is not null || updates is not null) && _visible is { } timeline)
        {
            var bottom = height - BandHeight;

            var markHeight = calls is not null && updates is not null ? LaneHeaderHeight / 2 : LaneHeaderHeight;

            canvas.Save();

            canvas.ClipRect(new SKRect(0, ContentTop, width, bottom));

            for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
            {
                var top = ContentTop + _laneTops[lane] - (float)_scrollY;

                if (top + LaneHeaderHeight < ContentTop || top > bottom)
                {
                    continue;
                }

                if (calls is not null && lane < calls.Length && calls[lane] is { } bars)
                {
                    DrawSearchMatches(canvas, bars, top, markHeight, scale);
                }

                if (updates is not null)
                {
                    DrawMemoryUpdates(canvas, updates, calls is null ? top : top + markHeight, markHeight, scale);
                }
            }

            canvas.Restore();
        }

        if (_searchTerms.Length > 0 && ShowsMemory && GetVisibleMemoryMatches() is { Count: > 0 } visible)
        {
            _memory.DrawSearchMatches(canvas, visible, _paints.SearchMatch, SearchMatchThickness, MinimumSearchMatchLength);
        }
    }

    private List<int> GetVisibleMemoryMatches()
    {
        var visible = new List<int>();

        foreach (var index in _memoryMatches)
        {
            var (from, to) = GetLifetime(index);

            if (from <= _viewEnd && to >= _viewStart)
            {
                visible.Add(index);
            }
        }

        return visible;
    }

    private (double From, double To) GetLifetime(int block)
    {
        var found = _memory.Blocks[block];

        return (TraceScale.GetValue(found.From, _axis), found.To is { } to ? TraceScale.GetValue(to, _axis) : FullEnd);
    }

    private bool ClickSearchMatch(Point position)
    {
        if (_searchTerms.Length == 0)
        {
            return false;
        }

        if (ShowsFlameChart && GetLaneHeaderAt(position) is { } lane)
        {
            var laneTop = ContentTop + _laneTops[lane] - _scrollY;

            var isSearchBand = GetMemoryUpdateMarks() is null || position.Y < laneTop + LaneHeaderHeight / 2;

            return isSearchBand && ZoomToCallMatches((float)position.X, lane);
        }

        if (ShowsMemory && _memory.Contains(position) && position.X < SearchMatchThickness + SearchMatchHitSlop)
        {
            return ZoomToMemoryMatches((float)position.Y);
        }

        return false;
    }

    private string? GetHeaderMarkToolTip(Point position)
    {
        if (!ShowsFlameChart || GetLaneHeaderAt(position) is not { } lane)
        {
            return null;
        }

        var calls = _searchTerms.Length > 0 ? GetCallMatchBars() : null;

        var updates = GetMemoryUpdateMarks();

        var laneTop = ContentTop + _laneTops[lane] - _scrollY;

        var isSearchBand = calls is not null && (updates is null || position.Y < laneTop + LaneHeaderHeight / 2);

        var x = (float)position.X;

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        if (isSearchBand)
        {
            return lane < calls!.Length && calls[lane] is { } bars && IsOverSearchBar(bars, x, scale) ? "Search Results" : null;
        }

        return updates is not null && IsOverMemoryUpdate(updates, x, scale) ? $"{_memoryUpdatesName} Updates" : null;
    }

    private string? GetMemorySearchToolTip(Point position)
    {
        if (_searchTerms.Length == 0 || position.X >= SearchMatchThickness + SearchMatchHitSlop)
        {
            return null;
        }

        var blocks = _memory.GetSearchMatchesAt((float)position.Y, GetVisibleMemoryMatches(), MinimumSearchMatchLength);

        return blocks.Count > 0 ? "Search Results" : null;
    }

    private bool IsOverSearchBar(SearchBars bars, float x, double scale)
    {
        var from = _viewStart + (x - HalfPixel - MinimumSearchMatchLength) / scale;

        var to = _viewStart + (x + HalfPixel) / scale;

        for (var index = SortedSearch.FirstAtOrAfter(bars.Ends, from); index < bars.Starts.Length && bars.Starts[index] <= to; index++)
        {
            var left = (float)((bars.Starts[index] - _viewStart) * scale);

            var right = Math.Max((float)((bars.Ends[index] - _viewStart) * scale), left + MinimumSearchMatchLength);

            if (left <= x + HalfPixel && right >= x - HalfPixel)
            {
                return true;
            }
        }

        return false;
    }

    private bool ZoomToCallMatches(float x, int lane)
    {
        if (_searchMatches is not { } matches || _visible is not { } timeline || lane >= timeline.Threads.Count)
        {
            return false;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        var from = _viewStart + (x - HalfPixel - MinimumSearchMatchLength) / scale;

        var to = _viewStart + (x + HalfPixel) / scale;

        FlameHit? only = null;

        var count = 0;

        var start = double.MaxValue;

        var end = double.MinValue;

        var rows = timeline.Threads[lane].Rows;

        for (var depth = 0; depth < rows.Count; depth++)
        {
            var row = rows[depth];

            for (var index = row.GetFirstEndingAfter(_axis, from);
                 index >= 0 && index < row.Count && row.GetStart(_axis, index) <= to;
                 index++)
            {
                if (!IsSearchMatch(matches, row.GetCallNodeIndex(index)) || !IsUnderSearchPixel(row, index, x, scale))
                {
                    continue;
                }

                count++;

                only = new FlameHit(lane, depth, index);

                start = Math.Min(start, row.GetStart(_axis, index));

                end = Math.Max(end, row.GetEnd(_axis, index));
            }
        }

        if (count == 0)
        {
            return false;
        }

        if (count == 1 && only is { } single)
        {
            ZoomToAndSelect(single);

            return true;
        }

        ZoomTo(start, end);

        return true;
    }

    private bool IsUnderSearchPixel(TraceTimelineRow row, int index, float x, double scale)
    {
        var left = (float)((row.GetStart(_axis, index) - _viewStart) * scale);

        var right = Math.Max((float)((row.GetEnd(_axis, index) - _viewStart) * scale), left + MinimumSearchMatchLength);

        return left <= x + HalfPixel && right >= x - HalfPixel;
    }

    private bool ZoomToMemoryMatches(float y)
    {
        var blocks = _memory.GetSearchMatchesAt(y, GetVisibleMemoryMatches(), MinimumSearchMatchLength);

        if (blocks.Count == 0)
        {
            return false;
        }

        var start = double.MaxValue;

        var end = double.MinValue;

        foreach (var block in blocks)
        {
            var (from, to) = GetLifetime(block);

            start = Math.Min(start, from);

            end = Math.Max(end, to);
        }

        ZoomTo(start, end);

        return true;
    }

    private void DrawSearchMatches(SKCanvas canvas, SearchBars bars, float top, float height, double scale)
    {
        float? left = null;

        var right = 0f;

        for (var index = SortedSearch.FirstAtOrAfter(bars.Ends, _viewStart);
             index < bars.Starts.Length && bars.Starts[index] <= _viewEnd;
             index++)
        {
            var start = (float)((bars.Starts[index] - _viewStart) * scale);

            var end = Math.Max((float)((bars.Ends[index] - _viewStart) * scale), start + MinimumSearchMatchLength);

            if (left is not null && start <= right)
            {
                right = Math.Max(right, end);

                continue;
            }

            if (left is { } drawn)
            {
                canvas.DrawRect(drawn, top, right - drawn, height, _paints.SearchMatch);
            }

            left = start;

            right = end;
        }

        if (left is { } last)
        {
            canvas.DrawRect(last, top, right - last, height, _paints.SearchMatch);
        }
    }

    private SearchBars?[]? GetCallMatchBars()
    {
        if (_searchMatches is not { } matches || _visible is not { } timeline)
        {
            return null;
        }

        if (_callMatchBars is { } cached
            && ReferenceEquals(cached.Matches, matches)
            && ReferenceEquals(cached.Timeline, timeline)
            && cached.Axis == _axis)
        {
            return cached.Lanes;
        }

        var lanes = new SearchBars?[timeline.Threads.Count];

        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var spans = new List<(double Start, double End)>();

            foreach (var row in timeline.Threads[lane].Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    if (IsSearchMatch(matches, row.GetCallNodeIndex(index)))
                    {
                        spans.Add((row.GetStart(_axis, index), row.GetEnd(_axis, index)));
                    }
                }
            }

            lanes[lane] = spans.Count > 0 ? SearchBars.Merge(spans) : null;
        }

        _callMatchBars = (matches, timeline, _axis, lanes);

        return lanes;
    }

    private double[]? GetMemoryMatchStarts()
    {
        if (_memoryMatches.Length == 0)
        {
            return null;
        }

        if (_memoryMatchStarts is { } cached && cached.Axis == _axis)
        {
            return cached.Starts;
        }

        double[] starts = [.. _memoryMatches.Select(i => TraceScale.GetValue(_memory.Blocks[i].From, _axis)).Order()];

        _memoryMatchStarts = (_axis, starts);

        return starts;
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

    private bool IsDimmed(int callNodeIndex) => Excludes(_memoryWriters, callNodeIndex);

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

    private sealed record SearchBars(double[] Starts, double[] Ends)
    {
        public static SearchBars Merge(List<(double Start, double End)> spans)
        {
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));

            var starts = new List<double>();

            var ends = new List<double>();

            foreach (var (start, end) in spans)
            {
                if (ends.Count > 0 && start <= ends[^1])
                {
                    ends[^1] = Math.Max(ends[^1], end);

                    continue;
                }

                starts.Add(start);

                ends.Add(end);
            }

            return new SearchBars([.. starts], [.. ends]);
        }
    }
}
