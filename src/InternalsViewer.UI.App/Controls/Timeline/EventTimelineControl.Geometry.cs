using System;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    // The selection only counts once the user has explicitly dragged a handle.
    private bool SelectionActive => _selectionActivated;

    // The active window the playhead is confined to and playback loops within: the from/to selection
    // when set, otherwise the whole axis.
    private (double Lo, double Hi) ActiveRange => SelectionActive
        ? (Math.Min(_startTime, _endTime), Math.Max(_startTime, _endTime))
        : (_minTime, _maxTime);

    private float CanvasWidth => (float)_overlay.ActualWidth;

    private float DrawWidth => CanvasWidth - RowLabelWidth;

    private double ContentWidth => DrawWidth * _zoom;

    private double MaxScroll => Math.Max(0, ContentWidth - DrawWidth);

    private float PlayheadX => TimeToX(_playheadTime);

    private float StartDrawX => SelectionActive ? TimeToX(_startTime) : TimeToX(_startTime) - HandleGap;
    private float EndDrawX => SelectionActive ? TimeToX(_endTime) : TimeToX(_endTime) + HandleGap;

    // EngineEvent.TimeUs / DurationUs are microseconds; divide by AxisUnitsPerMs (1000) to get ms.
    private static double StartMs(EngineEvent ev) => ev.TimeUs / AxisUnitsPerMs;

    private static double DurationMs(EngineEvent ev) => ev.DurationUs / AxisUnitsPerMs;

    // The axis works in milliseconds; events and the emitted playhead/scope are in microseconds.
    private static long ToUs(double ms) => (long)Math.Round(ms * 1000.0);

    private double EffectiveToMs(double effective) => effective - _minTime;

    // While not activated the handles sit on the playhead, so they follow it as it scrubs/plays.
    private void SyncHandlesToPlayhead()
    {
        if (!_selectionActivated)
        {
            _startTime = _playheadTime;
            _endTime = _playheadTime;
        }
    }

    private float TimeToX(double effectiveTimeMs)
        => RowLabelWidth + (float)((effectiveTimeMs - _minTime) / _timeRange * ContentWidth - _scrollX);

    private double XToTime(double x)
        => _minTime + (Math.Max(0, x - RowLabelWidth) + _scrollX) / ContentWidth * _timeRange;

    private void EnsurePlayheadVisible()
    {
        if (MaxScroll <= 0)
        {
            return;
        }

        var contentX = (_playheadTime - _minTime) / _timeRange * ContentWidth;

        const double margin = 24;

        if (contentX < _scrollX + margin)
        {
            _scrollX = Math.Clamp(contentX - margin, 0, MaxScroll);
        }
        else if (contentX > _scrollX + DrawWidth - margin)
        {
            _scrollX = Math.Clamp(contentX - DrawWidth + margin, 0, MaxScroll);
        }

        _scrollBar.Value = _scrollX;
    }

    private void ClampScroll() => _scrollX = Math.Clamp(_scrollX, 0, MaxScroll);

    private void UpdateScrollBar()
    {
        if (_zoom <= MinZoom + 1e-9 || MaxScroll <= 0 || DrawWidth <= 0)
        {
            _scrollBar.Visibility = Visibility.Collapsed;
            _scrollX = 0;

            return;
        }

        _scrollBar.Visibility = Visibility.Visible;
        _scrollBar.Maximum = MaxScroll;
        _scrollBar.ViewportSize = DrawWidth;
        _scrollBar.LargeChange = DrawWidth * 0.9;
        _scrollBar.SmallChange = DrawWidth * 0.1;
        _scrollBar.Value = _scrollX;
    }
}
