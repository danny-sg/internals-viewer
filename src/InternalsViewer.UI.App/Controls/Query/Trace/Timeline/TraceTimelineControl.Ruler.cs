using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl : ITimelineRulerSource
{
    private const double RulerFollowMargin = 24;

    private const string SearchResultsName = "Search Results";

    public event Action? RulerChanged;

    public TimelineRulerState? GetRulerState()
        => _visible is null
            ? null
            : new TimelineRulerState(_viewStart,
                                     _viewEnd,
                                     FullStart,
                                     FullEnd,
                                     OriginOf(_axis),
                                     0,
                                     _overlay.ActualWidth,
                                     _playhead,
                                     IsTrimmed ? TrimFrom : null,
                                     IsTrimmed ? TrimTo : null)
            {
                Cuts = _visible.GetCuts(_axis)
            };

    public double? GetRulerOffset(UIElement relativeTo) => TimelineRuler.GetContentOffset(_overlay, relativeTo);

    public string FormatRulerTick(double offset, double step) => FormatAxis(offset, step);

    public string FormatRulerPlayhead(double value) => PlayheadLabel(value);

    public List<TimelineRulerMarks> GetRulerMarks()
    {
        var marks = new List<TimelineRulerMarks>();

        if (_searchTerms.Length > 0 && GetCallMatchBars() is { } bars)
        {
            marks.Add(new TimelineRulerMarks(TimelineRulerMarkType.SearchResult,
                                             SearchResultsName,
                                             bars.Starts,
                                             bars.Ends,
                                             _paints.SearchMatch.Color,
                                             MinimumSearchMatchLength));
        }

        if (GetMemoryUpdateMarks() is { } updates)
        {
            marks.Add(new TimelineRulerMarks(TimelineRulerMarkType.Update,
                                             $"{_memoryUpdatesName} Updates",
                                             updates.Values,
                                             updates.Values,
                                             SKColors.Transparent,
                                             MemoryUpdateWidth)
            {
                Colours = updates.Colours
            });
        }

        return marks;
    }

    public bool ClickRulerMark(TimelineRulerMarkType type, double from, double to, double minimumLength)
        => type == TimelineRulerMarkType.SearchResult && ZoomToCallMatches(from, to, minimumLength);

    public void MoveRulerPlayhead(double value)
    {
        if (_visible is null)
        {
            return;
        }

        var playhead = ClampToTrim(Math.Clamp(value, FullStart, FullEnd));

        ShowPlayhead(playhead);

        KeepInView(playhead);

        HideToolTip();

        _canvas.Invalidate();

        ReportPlayhead();

        RulerChanged?.Invoke();
    }

    public void MoveRulerCrop(TimelineRulerHandle handle, double value)
    {
        if (_visible is null)
        {
            return;
        }

        _trimDrag = handle == TimelineRulerHandle.Start ? TrimHandle.Start : TrimHandle.End;

        DragTrimTo(value);

        RulerChanged?.Invoke();
    }

    public void EndRulerCrop()
    {
        if (IsDraggingTrim)
        {
            EndTrimDrag();
        }

        RulerChanged?.Invoke();
    }

    public void ResetRulerCrop()
    {
        if (IsTrimmed)
        {
            ClearTrim();
        }

        RulerChanged?.Invoke();
    }

    private void KeepInView(double value)
    {
        var range = _viewEnd - _viewStart;

        var width = _overlay.ActualWidth;

        var margin = width > RulerFollowMargin * 2 ? range * RulerFollowMargin / width : 0;

        if (value < _viewStart + margin)
        {
            ScrollViewTo(value - margin);
        }
        else if (value > _viewEnd - margin)
        {
            ScrollViewTo(value + margin - range);
        }
    }
}
