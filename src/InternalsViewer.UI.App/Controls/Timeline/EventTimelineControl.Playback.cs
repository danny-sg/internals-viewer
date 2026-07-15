using System;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private double _playheadTime;

    private double _playEndTime;

    private double _playStep;

    private bool _isPlaying;

    /// <summary>
    /// Raised when the playhead moves, with its position in microseconds
    /// </summary>
    public event Action<long>? PlayheadTimeChanged;

    /// <summary>
    /// Raised when auto-play starts (true) or stops (false)
    /// </summary>
    public event Action<bool>? PlayStateChanged;

    private void OnPlayPauseRequested()
    {
        if (_isPlaying)
        {
            StopPlay();
        }
        else
        {
            StartPlay();
        }
    }

    private void OnStepRequested(bool forward) => StepToAdjacentEvent(forward);

    private void OnThreadsToggled(bool showThreads)
    {
        _showThreads = showThreads;
        _skCanvas.Invalidate();
    }

#pragma warning disable VSTHRD100 // Avoid async void methods - Required for event and using try/catch for safety
    private async void OnAudioToggled(bool enabled)
    {
        try
        {
            IsAudioEnabled = enabled;

            if (enabled)
            {
                await _audioPlayer.EnsureInitializedAsync();
            }
        }
        catch
        {
            // No-op - Audio is non-critical
        }
    }
#pragma warning restore VSTHRD100

    private void StepToAdjacentEvent(bool forward)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var (lo, hi) = ActiveRange;

        // The nearest read (by time) on the requested side of the playhead, within the active range.
        var target = -1;
        var bestTime = forward ? double.MaxValue : double.MinValue;

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is not ReadEventGroup)
            {
                continue;
            }

            var t = _times[i];

            if (t < lo || t > hi)
            {
                continue;
            }

            if (forward ? t > _playheadTime && t < bestTime
                        : t < _playheadTime && t > bestTime)
            {
                bestTime = t;
                target = i;
            }
        }

        if (target < 0)
        {
            return;
        }

        _playheadTime = _times[target];

        SyncHandlesToPlayhead();

        FirePlayhead();

        EventSelected?.Invoke(_sortedEvents[target]);

        EnsurePlayheadVisible();

        _skCanvas.Invalidate();
    }

    private void StartPlay()
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var (rangeStart, rangeEnd) = ActiveRange;

        _playEndTime = rangeEnd;

        if (_playheadTime < rangeStart || _playheadTime >= rangeEnd)
        {
            _playheadTime = rangeStart;
        }

        _isPlaying = true;

        var rangeMs = Math.Max(rangeEnd - rangeStart, 1e-6);

        _playStep = rangeMs * PlayTickMs / BasePlayDurationMs;

        SyncHandlesToPlayhead();
        FirePlayhead();

        _transport.SetPlaying(isPlaying: true);

        _playTimer.Start();

        PlayStateChanged?.Invoke(true);

        _skCanvas.Invalidate();
    }

    private void StopPlay()
    {
        var wasPlaying = _isPlaying;

        _playTimer.Stop();

        _isPlaying = false;

        _transport.SetPlaying(isPlaying: false);

        if (wasPlaying)
        {
            PlayStateChanged?.Invoke(false);
        }
    }

    private void OnPlayTimerTick(object? sender, object e)
    {
        _playheadTime += _playStep;

        if (_playheadTime >= _playEndTime)
        {
            _playheadTime = _playEndTime;

            SyncHandlesToPlayhead();
            FirePlayhead();

            _skCanvas.Invalidate();

            StopPlay();

            return;
        }

        SyncHandlesToPlayhead();

        FirePlayhead();

        _skCanvas.Invalidate();
    }

    /// <summary>
    /// Emits the in-scope time window (microseconds)
    /// </summary>
    private void EmitScope()
    {
        long fromUs, toUs;

        if (SelectionActive)
        {
            fromUs = ToUs(Math.Min(_startTime, _endTime));
            toUs = ToUs(Math.Max(_startTime, _endTime));
        }
        else
        {
            fromUs = ToUs(_minTime);
            toUs = ToUs(_playheadTime);
        }

        // Smooth play ticks many times per pixel; only notify when the scope actually changes.
        if (fromUs == _scopeFromUs && toUs == _scopeToUs)
        {
            return;
        }

        _scopeFromUs = fromUs;
        _scopeToUs = toUs;
        ScopeChanged?.Invoke(fromUs, toUs);
    }

    private void FirePlayhead()
    {
        // Emit the scope first so a consumer reacting to the playhead sees the current window
        EmitScope();

        var playheadUs = ToUs(_playheadTime);

        if (playheadUs != _playheadUs)
        {
            var previousUs = _playheadUs;
            _playheadUs = playheadUs;
            PlayheadTimeChanged?.Invoke(playheadUs);

            if (IsAudioEnabled)
            {
                PlayAudioForCurrentPosition(previousUs, playheadUs);
            }
        }
    }

    private void PlayAudioForCurrentPosition(long fromUs, long toUs)
    {
        var lo = Math.Min(fromUs, toUs);
        var hi = Math.Max(fromUs, toUs);

        SweepEvents(_readEventsByTime, lo, hi,
                    io => _audioPlayer.PlayPlink(TimelineAudioPlayer.FrequencyForObject(io.ObjectId)));

        SweepEvents(_latchEventsByTime, lo, hi,
                    latch => _audioPlayer.PlayLatchTick(TimelineAudioPlayer.FrequencyForObject(latch.ObjectId)));

        SweepEvents(_fileReadEventsByTime, lo, hi, _ => _audioPlayer.PlayFileRumble());
    }

    private static void SweepEvents<T>(T[] eventsByTime, long lo, long hi, Action<T> play) where T : EngineEvent
    {
        if (eventsByTime.Length == 0)
        {
            return;
        }

        // Binary search for the first event with TimeUs > lo.
        var left = 0;
        var right = eventsByTime.Length - 1;

        while (left < right)
        {
            var mid = (left + right) >> 1;

            if (eventsByTime[mid].TimeUs <= lo)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        if (eventsByTime[left].TimeUs <= lo)
        {
            return;
        }

        for (var i = left; i < eventsByTime.Length; i++)
        {
            var ev = eventsByTime[i];

            if (ev.TimeUs > hi)
            {
                break;
            }

            play(ev);
        }
    }
}
