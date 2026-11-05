using System;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float MemoryUpdateWidth = 1;

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

        RulerChanged?.Invoke();
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

    private sealed record MemoryUpdateMarks(double[] Values, SKColor[] Colours);
}
