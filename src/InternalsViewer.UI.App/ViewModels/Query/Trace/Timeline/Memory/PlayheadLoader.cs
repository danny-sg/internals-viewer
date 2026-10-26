using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

internal sealed class PlayheadLoader<T>(TimeSpan interval, Action<T> apply, Action<bool>? busyChanged = null)
{
    private Func<T>? Requested { get; set; }

    private CancellationTokenSource Cancellation { get; set; } = new();

    private bool IsRunning { get; set; }

    private long LastLoaded { get; set; }

    public void Request(Func<T> read)
    {
        Requested = read;

        if (IsRunning)
        {
            return;
        }

        IsRunning = true;

        busyChanged?.Invoke(true);

        _ = RunAsync(Cancellation.Token);
    }

    public void Cancel()
    {
        Requested = null;

        if (!IsRunning)
        {
            return;
        }

        Cancellation.Cancel();

        Cancellation = new CancellationTokenSource();

        IsRunning = false;

        busyChanged?.Invoke(false);
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (Requested is not null)
            {
                var wait = interval - Stopwatch.GetElapsedTime(LastLoaded);

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, token);
                }

                if (Requested is not { } read)
                {
                    break;
                }

                Requested = null;

                var result = await Task.Run(read, token);

                LastLoaded = Stopwatch.GetTimestamp();

                token.ThrowIfCancellationRequested();

                apply(result);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsRunning = false;

                busyChanged?.Invoke(false);
            }
        }
    }
}
