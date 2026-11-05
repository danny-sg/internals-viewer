using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const double PlayTickMs = 16;

    private const double SweepDurationMs = 30_000;

    private DispatcherTimer? _playTimer;

    public event Action<bool>? PlayStateChanged;

    public event Action? StepModeChanged;

    public bool IsPlaying => _playTimer?.IsEnabled == true;

    public double PlaySpeed { get; set; } = 1;

    public PlayheadStepMode StepMode { get; private set; } = PlayheadStepMode.Call;

    public string? StepTrack { get; private set; }

    public void Step(bool forward)
    {
        StepPlayhead(forward, StepMode, StepTrack);

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

    public void SetStepMode(PlayheadStepMode mode, string? track)
    {
        StepMode = mode;

        StepTrack = mode == PlayheadStepMode.Track ? track : null;

        StepModeChanged?.Invoke();
    }

    public IEnumerable<TraceTickTrack> GetStepTracks()
        => (TickTracks ?? []).Where(t => t.Series.Count > 0 && HiddenTickTracks?.Contains(t.Name) != true);

    public TraceTickTrack? GetStepTrack(string? name)
        => GetStepTracks().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private void ResetHiddenStepTrack()
    {
        if (StepMode == PlayheadStepMode.Track && GetStepTrack(StepTrack) is null)
        {
            SetStepMode(PlayheadStepMode.Call, null);
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
