using System;
using System.Collections.Generic;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Models.Query.Timeline;

namespace InternalsViewer.UI.App.Controls.Query.Timeline;

internal static class TimelineRulerLayout
{
    public static double GetX(TimelineRulerState state, TimelineRulerArea area, double value)
        => area.Left + (value - state.ViewStart) / GetViewLength(state) * area.Width;

    public static double GetValue(TimelineRulerState state, TimelineRulerArea area, double x)
    {
        var value = area.Width <= 0 ? state.ViewStart : state.ViewStart + (x - area.Left) / area.Width * GetViewLength(state);

        return Math.Clamp(value, state.FullStart, Math.Max(state.FullStart, state.FullEnd));
    }

    public static (double? Start, double? End) GetHandleXs(TimelineRulerState state, TimelineRulerArea area, double gap)
    {
        if (state is { CropStart: { } start, CropEnd: { } end })
        {
            return (GetX(state, area, start), GetX(state, area, end));
        }

        if (state.Playhead is not { } playhead)
        {
            return (null, null);
        }

        var x = GetX(state, area, playhead);

        return (x - gap, x + gap);
    }

    public static (double Step, List<double> Values) GetTicks(TimelineRulerState state, TimelineRulerArea area, double spacing)
    {
        var lower = Math.Max(state.ViewStart, state.FullStart);

        var upper = Math.Min(state.ViewEnd, state.FullEnd);

        var step = area.Width <= 0 ? 0 : TimelineFormat.NiceInterval(spacing * GetViewLength(state) / area.Width);

        var values = new List<double>();

        if (step <= 0 || upper < lower)
        {
            return (step, values);
        }

        for (var index = Math.Ceiling((lower - state.Origin) / step); state.Origin + index * step <= upper; index++)
        {
            values.Add(state.Origin + index * step);
        }

        return (step, values);
    }

    public static double GetEdgeStep(TimelineRulerState state, TimelineRulerArea area, double x, double zone, double pixels)
    {
        if (zone <= 0 || area.Width <= zone * 2)
        {
            return 0;
        }

        var unitsPerPixel = GetViewLength(state) / area.Width;

        var left = (area.Left + zone - x) / zone;

        if (left > 0 && state.ViewStart > state.FullStart)
        {
            return -Math.Min(left, 1) * pixels * unitsPerPixel;
        }

        var right = (x - (area.Right - zone)) / zone;

        if (right > 0 && state.ViewEnd < state.FullEnd)
        {
            return Math.Min(right, 1) * pixels * unitsPerPixel;
        }

        return 0;
    }

    public static double GetLength(TimelineRulerState state, TimelineRulerArea area, double pixels)
        => area.Width <= 0 ? 0 : pixels * GetViewLength(state) / area.Width;

    public static bool IsOverMark(TimelineRulerMarks marks, double from, double to, double minimumLength)
    {
        for (var index = SortedSearch.FirstAtOrAfter(marks.Ends, from - minimumLength);
             index < marks.Starts.Length && marks.Starts[index] <= to;
             index++)
        {
            if (Math.Max(marks.Ends[index], marks.Starts[index] + minimumLength) >= from)
            {
                return true;
            }
        }

        return false;
    }

    private static double GetViewLength(TimelineRulerState state) => Math.Max(state.ViewEnd - state.ViewStart, double.Epsilon);
}
