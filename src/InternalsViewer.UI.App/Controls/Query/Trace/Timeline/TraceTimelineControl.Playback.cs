using System;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const double PlayTickMs = 16;

    private const double SweepDurationMs = 30_000;

    private DispatcherTimer? _playTimer;

    public event Action<bool>? PlayStateChanged;

    public bool IsPlaying => _playTimer?.IsEnabled == true;

    public double PlaySpeed { get; set; } = 1;

    public PlayheadStepMode StepMode { get; set; } = PlayheadStepMode.Call;

    public void Step(bool forward)
    {
        StepPlayhead(forward, StepMode);

        ReportPlayhead();

        RulerChanged?.Invoke();
    }

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            StopPlay();
        }
        else
        {
            StartPlay();
        }
    }

    private void StartPlay()
    {
        if (_visible is null)
        {
            return;
        }

        if (_playhead is not { } playhead || playhead >= TrimTo || playhead < TrimFrom)
        {
            ShowPlayhead(_playhead is null ? GetSelectedStart() ?? ClampToTrim(_viewStart) : ClampToTrim(_viewStart));
        }

        if (_playTimer is null)
        {
            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PlayTickMs) };

            _playTimer.Tick += OnPlayTick;
        }

        _playTimer.Start();

        PlayStateChanged?.Invoke(true);

        _canvas.Invalidate();
    }

    private void StopPlay()
    {
        if (_playTimer is not { IsEnabled: true } timer)
        {
            return;
        }

        timer.Stop();

        PlayStateChanged?.Invoke(false);
    }

    private void OnPlayTick(object? sender, object e)
    {
        if (_playhead is not { } playhead || _visible is null)
        {
            StopPlay();

            return;
        }

        var range = _viewEnd - _viewStart;

        var step = range * PlayTickMs / SweepDurationMs * PlaySpeed;

        var next = IsTrimmed && playhead >= TrimTo ? TrimFrom : Math.Min(playhead + step, TrimTo);

        _playhead = next;

        ClearPins();

        if (next > _viewEnd || next < _viewStart)
        {
            ScrollViewTo(next);
        }

        _canvas.Invalidate();

        ReportPlayhead();

        RulerChanged?.Invoke();

        if (next >= TrimTo && !IsTrimmed)
        {
            StopPlay();
        }
    }
}
