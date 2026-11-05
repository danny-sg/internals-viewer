using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Controls.Query.Timeline;

public sealed partial class EventTimelineControl : ITimelineRulerSource
{
    private const int TimeTextLength = 12;

    public event Action? RulerChanged;

    public TimelineRulerState? GetRulerState()
    {
        if (_definition.Events.Count == 0)
        {
            return null;
        }

        var hasView = ContentWidth > 0 && CanvasWidth > BandLabelWidth;

        var (cropStart, cropEnd) = ActiveRange;

        return new TimelineRulerState(hasView ? XToTime(BandLabelWidth) : _minTime,
                                      hasView ? XToTime(CanvasWidth) : _maxTime,
                                      _minTime,
                                      _maxTime,
                                      _minTime,
                                      BandLabelWidth,
                                      CanvasWidth,
                                      _playheadTime,
                                      SelectionActive ? cropStart : null,
                                      SelectionActive ? cropEnd : null);
    }

    public double? GetRulerOffset(UIElement relativeTo) => TimelineRuler.GetContentOffset(_overlay, relativeTo);

    public string FormatRulerTick(double offset, double step) => FormatMilliseconds(offset);

    public string FormatRulerPlayhead(double value) => FormatMilliseconds(EffectiveToMs(value));

    public List<TimelineRulerMarks> GetRulerMarks() => [];

    public bool ClickRulerMark(TimelineRulerMarkType type, double from, double to, double minimumLength) => false;

    public void MoveRulerPlayhead(double value)
    {
        if (_definition.Events.Count == 0)
        {
            return;
        }

        MovePlayheadToTime(value);

        EnsurePlayheadVisible();

        _skCanvas.Invalidate();

        RulerChanged?.Invoke();
    }

    public void MoveRulerCrop(TimelineRulerHandle handle, double value)
    {
        if (_definition.Events.Count == 0)
        {
            return;
        }

        MoveSelectionHandle(handle, value);

        _skCanvas.Invalidate();

        RulerChanged?.Invoke();
    }

    public void EndRulerCrop() => RulerChanged?.Invoke();

    public void ResetRulerCrop()
    {
        DeactivateSelection();

        _skCanvas.Invalidate();

        RulerChanged?.Invoke();
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        Span<char> text = stackalloc char[TimeTextLength];

        var length = TimelineFormat.FormatTimeIntoSpan(milliseconds, text);

        return new string(text[..length]);
    }

    private static void OnIsRulerPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventTimelineControl)d;

        control._transport.IsPlaybackVisible = !control.IsRulerPinned;
    }
}
