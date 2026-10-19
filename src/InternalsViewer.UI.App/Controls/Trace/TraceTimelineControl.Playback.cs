using System;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const double PlayTickMs = 16;

    private const double SweepDurationMs = 30_000;

    private DispatcherTimer? _playTimer;

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
            ScrollViewTo(next);
        }

        _canvas.Invalidate();

        if (next >= FullEnd)
        {
            StopPlay();
        }
    }
}
