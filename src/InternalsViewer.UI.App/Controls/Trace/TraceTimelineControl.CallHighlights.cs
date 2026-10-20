using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const byte CallHighlightAlpha = 56;

    private static readonly CallHighlight[] CallHighlights =
    [
        new("CQuery::CreateExecPlan", SKColors.LightGreen),
        new("CXStmtQuery::ShutdownNormal", SKColors.LightCoral)
    ];

    public static readonly DependencyProperty ShowHighlightRegionsProperty =
        DependencyProperty.Register(nameof(ShowHighlightRegions),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnShowHighlightRegionsChanged));

    public bool ShowHighlightRegions
    {
        get => (bool)GetValue(ShowHighlightRegionsProperty);
        set => SetValue(ShowHighlightRegionsProperty, value);
    }

    private HighlightBand _positionHighlights = HighlightBand.Empty;

    private HighlightBand _instructionHighlights = HighlightBand.Empty;

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

        for (var node = 0; node < highlights.Length; node++)
        {
            highlights[node] = timeline.NodeOf(node) is { } callNode ? HighlightOf(callNode.Symbol) : -1;

            any |= highlights[node] >= 0;
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
                    var node = row.NodeAt(index);

                    if (node < 0 || node >= highlights.Length || highlights[node] < 0)
                    {
                        continue;
                    }

                    regions.Add(new HighlightRegion(depth,
                                                    highlights[node],
                                                    row.Span(TraceTimelineAxis.Position, index),
                                                    row.Span(TraceTimelineAxis.Instructions, index)));
                }
            }
        }

        _positionHighlights = BandOf(regions, TraceTimelineAxis.Position);

        _instructionHighlights = BandOf(regions, TraceTimelineAxis.Instructions);
    }

    private void DrawCallHighlights(SKCanvas canvas, float top, float bottom, int width, double scale)
    {
        if (!ShowHighlightRegions)
        {
            return;
        }

        var band = _axis == TraceTimelineAxis.Position ? _positionHighlights : _instructionHighlights;

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

    private static int HighlightOf(string symbol)
    {
        for (var index = 0; index < CallHighlights.Length; index++)
        {
            if (CallHighlights[index].Name.Equals(symbol, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static HighlightBand BandOf(List<HighlightRegion> regions, TraceTimelineAxis axis)
    {
        var boundaries = new double[regions.Count * 2];

        for (var index = 0; index < regions.Count; index++)
        {
            var span = regions[index].Of(axis);

            boundaries[index * 2] = span.Start;

            boundaries[index * 2 + 1] = span.End;
        }

        Array.Sort(boundaries);

        regions.Sort((a, b) => a.Of(axis).Start.CompareTo(b.Of(axis).Start));

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

            while (next < regions.Count && regions[next].Of(axis).Start <= from)
            {
                active.Add(regions[next]);

                next++;
            }

            for (var position = active.Count - 1; position >= 0; position--)
            {
                if (active[position].Of(axis).End <= from)
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

    private static void OnShowHighlightRegionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TraceTimelineControl)d).Redraw();

    private readonly record struct CallHighlight(string Name, SKColor Colour);

    private readonly record struct HighlightRegion(int Depth, int Highlight, TraceSpan Position, TraceSpan Instructions)
    {
        public TraceSpan Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
    }

    private sealed record HighlightBand(double[] Starts, double[] Ends, int[] Highlights)
    {
        public static HighlightBand Empty { get; } = new([], [], []);
    }
}
