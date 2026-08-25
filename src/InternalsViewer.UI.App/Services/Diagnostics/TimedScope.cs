using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.Services.Diagnostics;

/// <summary>
/// Times a stretch of work and writes what it took to the app log
/// </summary>
/// <remarks>
/// Taken through <see cref="LoggerTimingExtensions.Time"/> so nothing is measured, allocated or written while the
/// log is above debug. Timings are read in the App Log tab with its level turned down to Debug.
/// </remarks>
public sealed class TimedScope(ILogger logger, string name, string? detail) : IDisposable
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public void Dispose()
    {
        var elapsed = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;

        if (detail is null)
        {
            logger.LogDebug("{Name} took {Elapsed:0.0} ms", name, elapsed);

            return;
        }

        logger.LogDebug("{Name} ({Detail}) took {Elapsed:0.0} ms", name, detail, elapsed);
    }
}

public static class LoggerTimingExtensions
{
    /// <summary>
    /// Measures the scope it is used in, or does nothing at all when timings are not being collected
    /// </summary>
    /// <remarks>
    /// The result is disposed by a using statement, a null one being a using over nothing.
    /// </remarks>
    public static IDisposable? Time(this ILogger logger, string name, string? detail = null)
        => logger.IsEnabled(LogLevel.Debug) ? new TimedScope(logger, name, detail) : null;

    /// <summary>
    /// Measures from here until the interface has nothing left to do, which is the length of a pause
    /// </summary>
    /// <remarks>
    /// Inflating markup, laying it out, generating containers and drawing all run at a higher priority than this
    /// callback, so it is reached once they are done rather than once the call that started them returned.
    /// </remarks>
    public static void TimeUntilIdle(this ILogger logger, DispatcherQueue queue, string name, string? detail = null)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();

        queue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            if (detail is null)
            {
                logger.LogDebug("{Name} settled after {Elapsed:0.0} ms", name, elapsed);

                return;
            }

            logger.LogDebug("{Name} ({Detail}) settled after {Elapsed:0.0} ms", name, detail, elapsed);
        });
    }
}
