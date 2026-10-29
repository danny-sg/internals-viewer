using System;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private TraceMemoryWrites? _memoryUpdates;

    private string? _memoryUpdatesName;

    private (TraceMemoryWrites Writes, TraceTimeline Timeline, TraceTimelineAxis Axis, MemoryUpdateMarks Marks)? _memoryUpdateMarks;

    public void ShowMemoryUpdates(TraceMemoryWrites? writes, string? name)
    {
        _memoryUpdatesName = name;

        if (ReferenceEquals(writes, _memoryUpdates))
        {
            return;
        }

        _memoryUpdates = writes;

        _canvas.Invalidate();
    }

    private MemoryUpdateMarks? GetMemoryUpdateMarks()
    {
        if (_memoryUpdates is not { Count: > 0 } writes || _timeline is not { } timeline)
        {
            return null;
        }

        if (_memoryUpdateMarks is { } cached
            && ReferenceEquals(cached.Writes, writes)
            && ReferenceEquals(cached.Timeline, timeline)
            && cached.Axis == _axis)
        {
            return cached.Marks;
        }

        var alpha = _paints.SearchMatch.Color.Alpha;

        var values = new double[writes.Count];

        var colours = new SKColor[writes.Count];

        for (var index = 0; index < writes.Count; index++)
        {
            values[index] = TraceScale.GetValue(writes.GetPoint(index), _axis);

            colours[index] = GetCategoryColour(writes.GetCallNodeIndex(index)).WithAlpha(alpha);
        }

        Array.Sort(values, colours);

        var marks = new MemoryUpdateMarks(values, colours);

        _memoryUpdateMarks = (writes, timeline, _axis, marks);

        return marks;
    }

    private void DrawMemoryUpdates(SKCanvas canvas, MemoryUpdateMarks marks, float top, float height, double scale)
    {
        var lastPixel = int.MinValue;

        var lastColour = SKColors.Transparent;

        for (var index = SortedSearch.FirstAtOrAfter(marks.Values, _viewStart - MinimumSearchMatchLength / scale);
             index < marks.Values.Length && marks.Values[index] <= _viewEnd;
             index++)
        {
            var x = (float)((marks.Values[index] - _viewStart) * scale);

            var pixel = (int)MathF.Floor(x);

            var colour = marks.Colours[index];

            if (pixel <= lastPixel + 1 && colour == lastColour)
            {
                continue;
            }

            lastPixel = pixel;

            lastColour = colour;

            _paints.Fill.Color = colour;

            canvas.DrawRect(x - MinimumSearchMatchLength / 2, top, MinimumSearchMatchLength, height, _paints.Fill);
        }
    }

    private bool IsOverMemoryUpdate(MemoryUpdateMarks marks, float x, double scale)
    {
        var reach = HalfPixel + MinimumSearchMatchLength / 2;

        var index = SortedSearch.FirstAtOrAfter(marks.Values, _viewStart + (x - reach) / scale);

        return index < marks.Values.Length && marks.Values[index] <= _viewStart + (x + reach) / scale;
    }

    private sealed record MemoryUpdateMarks(double[] Values, SKColor[] Colours);
}
