using System;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class GuideTransport(Func<bool, bool> step) : ITimelineTransport, IDisposable
{
    public event Action<bool>? PlayStateChanged;

    public bool IsPlaying => Timer?.IsRunning == true;

    public double PlaySpeed { get; set; } = 1;

    private DispatcherQueueTimer? Timer { get; set; }

    private TimeSpan Hold => TimeSpan.FromMilliseconds(ITimelineTransport.StepHoldMs
                                                       / Math.Max(PlaySpeed, ITimelineTransport.MinimumPlaySpeed));

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void Step(bool forward)
    {
        Stop();

        step(forward);
    }

    public void Stop()
    {
        if (Timer is not { IsRunning: true } timer)
        {
            return;
        }

        timer.Stop();

        PlayStateChanged?.Invoke(false);
    }

    public void Dispose()
    {
        Stop();

        if (Timer is { } timer)
        {
            timer.Tick -= OnTick;
        }

        Timer = null;
    }

    private void Start()
    {
        if (!step(true))
        {
            return;
        }

        if (Timer is null)
        {
            Timer = DispatcherQueue.GetForCurrentThread().CreateTimer();

            Timer.Tick += OnTick;
        }

        Timer.Interval = Hold;

        Timer.Start();

        PlayStateChanged?.Invoke(true);
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        sender.Interval = Hold;

        if (!step(true))
        {
            Stop();
        }
    }
}
