using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const double PlayTickMs = 16;

    private const double SweepDurationMs = 30_000;

    private const double TransitionMs = 250;

    private DispatcherTimer? _playTimer;

    private DispatcherTimer? _transitionTimer;

    private double _transitionFrom;

    private double _transitionTo;

    private long _transitionStart;

    public event Action<bool>? PlayStateChanged;

    public bool IsPlaying => _playTimer?.IsEnabled == true;

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

        StopTransition();

        if (_playhead is not { } playhead || playhead >= FullEnd)
        {
            _playhead = Math.Max(_viewStart, FullStart);
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

        var next = Math.Min(playhead + range * PlayTickMs / SweepDurationMs, FullEnd);

        _playhead = next;

        if (next > _viewEnd)
        {
            SetView(next, next + range);
        }

        _canvas.Invalidate();

        if (next >= FullEnd)
        {
            StopPlay();
        }
    }

    private void TransitionPlayhead(double target)
    {
        if (_playhead is not { } from || from == target)
        {
            StopTransition();

            PlacePlayhead(target);

            return;
        }

        _transitionFrom = from;

        _transitionTo = target;

        _transitionStart = Stopwatch.GetTimestamp();

        if (_transitionTimer is null)
        {
            _transitionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PlayTickMs) };

            _transitionTimer.Tick += OnTransitionTick;
        }

        _transitionTimer.Start();
    }

    private void StopTransition() => _transitionTimer?.Stop();

    private void OnTransitionTick(object? sender, object e)
    {
        var progress = Math.Min(Stopwatch.GetElapsedTime(_transitionStart).TotalMilliseconds / TransitionMs, 1);

        PlacePlayhead(_transitionFrom + (_transitionTo - _transitionFrom) * progress);

        if (progress >= 1)
        {
            StopTransition();
        }
    }
}
