using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const byte CallHighlightAlpha = 56;

    private static readonly CallHighlight[] CallHighlights =
    [
        new("CSQLSource::Transform", "Compilation", SKColors.LightGreen),
        new("CQuery::CreateExecPlan", "Setup", SKColors.LightSkyBlue),
        new(TraceTimeline.ShutdownSymbol, "Shutdown", SKColors.LightCoral)
    ];

    private readonly HashSet<string> _hiddenHighlightRegions = [];

    private HighlightBand _positionHighlights = HighlightBand.Empty;

    private HighlightBand _instructionHighlights = HighlightBand.Empty;

    public static IReadOnlyList<string> HighlightRegionLabels { get; } = Array.ConvertAll(CallHighlights, h => h.Label);

    public bool IsHighlightRegionVisible(string label) => !_hiddenHighlightRegions.Contains(label);

    public void SetHighlightRegionVisible(string label, bool isVisible)
    {
        var changed = isVisible ? _hiddenHighlightRegions.Remove(label) : _hiddenHighlightRegions.Add(label);

        if (!changed)
        {
            return;
        }

        FindCallHighlights();

        Redraw();
    }

    private MenuFlyoutSubItem CreateHighlightRegionsMenu()
        => MenuItems.SubMenu("Highlight Regions",
                             [.. HighlightRegionLabels.Select(label => MenuItems.Toggle(label,
                                                                                        IsHighlightRegionVisible(label),
                                                                                        isChecked => SetHighlightRegionVisible(label, isChecked)))]);

    private void FindCallHighlights()
    {
        _positionHighlights = HighlightBand.Empty;

        _instructionHighlights = HighlightBand.Empty;

        if (_timeline is not { } timeline)
        {
            return;
        }

        var highlights = new int[timeline.NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < highlights.Length; callNodeIndex++)
        {
            highlights[callNodeIndex] = timeline.NodeOf(callNodeIndex) is { } callNode ? HighlightOf(callNode.Symbol) : -1;

            any |= highlights[callNodeIndex] >= 0;
        }

        if (!any)
        {
            return;
        }

        var regions = new List<HighlightRegion>();

        foreach (var thread in timeline.Threads)
        {
            for (var depth = 0; depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                for (var index = 0; index < row.Count; index++)
                {
                    var callNodeIndex = row.GetCallNodeIndex(index);

                    if (callNodeIndex < 0 || callNodeIndex >= highlights.Length || highlights[callNodeIndex] < 0)
                    {
                        continue;
                    }

                    regions.Add(new HighlightRegion(depth, highlights[callNodeIndex], row.GetSpan(index)));
                }
            }
        }

        _positionHighlights = BandOf(regions, TraceTimelineAxis.Position, timeline.Scale);

        _instructionHighlights = BandOf(regions, TraceTimelineAxis.Instructions, timeline.Scale);
    }

    private void DrawCallHighlights(SKCanvas canvas, TraceTimeline timeline, float bottom, int width, double scale)
    {
        var band = _axis == TraceTimelineAxis.Position ? _positionHighlights : _instructionHighlights;

        if (band.Starts.Length == 0)
        {
            return;
        }

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var laneTop = ContentTop + _laneTops[lane] - (float)_scrollY;

            var rowsTop = laneTop + LaneHeaderHeight;

            var laneBottom = laneTop + LaneHeight(timeline.Threads[lane]) - LaneGap;

            if (laneBottom <= ContentTop || rowsTop >= bottom)
            {
                continue;
            }

            DrawHighlightBand(canvas, band, rowsTop, Math.Min(laneBottom, bottom), width, scale);
        }
    }

    private void DrawHighlightBand(SKCanvas canvas, HighlightBand band, float top, float bottom, int width, double scale)
    {
        var index = SortedSearch.FirstAfter(band.Ends, _viewStart);

        while (index < band.Starts.Length && band.Starts[index] <= _viewEnd)
        {
            var left = Math.Max((float)((band.Starts[index] - _viewStart) * scale), -1f);

            var right = Math.Min(Math.Max((float)((band.Ends[index] - _viewStart) * scale), left + NarrowSpan), width + 1f);

            _paints.Fill.Color = CallHighlights[band.Highlights[index]].Colour.WithAlpha(CallHighlightAlpha);

            canvas.DrawRect(left, top, right - left, bottom - top, _paints.Fill);

            index = SortedSearch.FirstAfter(band.Ends, _viewStart + (MathF.Floor(right) + 1f) / scale, index + 1);
        }
    }

    private int HighlightOf(string symbol)
    {
        for (var index = 0; index < CallHighlights.Length; index++)
        {
            if (CallHighlights[index].Name.Equals(symbol, StringComparison.Ordinal))
            {
                return _hiddenHighlightRegions.Contains(CallHighlights[index].Label) ? -1 : index;
            }
        }

        return -1;
    }

    private static HighlightBand BandOf(List<HighlightRegion> regions, TraceTimelineAxis axis, TraceScale scale)
    {
        var boundaries = new double[regions.Count * 2];

        for (var index = 0; index < regions.Count; index++)
        {
            boundaries[index * 2] = scale.GetValue(regions[index].Span.From, axis);

            boundaries[index * 2 + 1] = scale.GetValue(regions[index].Span.To, axis);
        }

        Array.Sort(boundaries);

        regions.Sort((a, b) => a.Span.From.CompareTo(b.Span.From, axis));

        var active = new List<HighlightRegion>();

        var starts = new List<double>();

        var ends = new List<double>();

        var highlights = new List<int>();

        var next = 0;

        for (var index = 0; index + 1 < boundaries.Length; index++)
        {
            var from = boundaries[index];

            var to = boundaries[index + 1];

            if (to <= from)
            {
                continue;
            }

            while (next < regions.Count && scale.GetValue(regions[next].Span.From, axis) <= from)
            {
                active.Add(regions[next]);

                next++;
            }

            for (var position = active.Count - 1; position >= 0; position--)
            {
                if (scale.GetValue(active[position].Span.To, axis) <= from)
                {
                    active.RemoveAt(position);
                }
            }

            if (active.Count == 0)
            {
                continue;
            }

            var highlight = InnermostOf(active);

            if (highlights.Count > 0 && highlights[^1] == highlight && ends[^1] == from)
            {
                ends[^1] = to;
            }
            else
            {
                starts.Add(from);

                ends.Add(to);

                highlights.Add(highlight);
            }
        }

        return new HighlightBand([.. starts], [.. ends], [.. highlights]);
    }

    private static int InnermostOf(List<HighlightRegion> active)
    {
        var innermost = active[0];

        foreach (var region in active)
        {
            if (region.Depth > innermost.Depth || (region.Depth == innermost.Depth && region.Highlight < innermost.Highlight))
            {
                innermost = region;
            }
        }

        return innermost.Highlight;
    }

    private readonly record struct CallHighlight(string Name, string Label, SKColor Colour);

    private readonly record struct HighlightRegion(int Depth, int Highlight, TraceSpan Span);

    private sealed record HighlightBand(double[] Starts, double[] Ends, int[] Highlights)
    {
        public static HighlightBand Empty { get; } = new([], [], []);
    }
}
