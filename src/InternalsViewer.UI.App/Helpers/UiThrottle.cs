using System;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.Helpers;

public sealed class UiThrottle : IDisposable
{
    public UiThrottle(DispatcherQueue queue, TimeSpan interval)
    {
        Timer = queue.CreateTimer();

        Timer.Interval = interval;

        Timer.IsRepeating = false;

        Timer.Tick += OnTick;
    }

    private DispatcherQueueTimer Timer { get; }

    private Action? Pending { get; set; }

    public void Run(Action action)
    {
        if (Timer.IsRunning)
        {
            Pending = action;

            return;
        }

        action();

        Timer.Start();
    }

    public void Dispose()
    {
        Timer.Stop();

        Timer.Tick -= OnTick;

        Pending = null;
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (Pending is not { } action)
        {
            return;
        }

        Pending = null;

        action();

        Timer.Start();
    }
}
