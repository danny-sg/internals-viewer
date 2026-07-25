using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Connection.BackupFile.Progress;

/// <summary>
/// Reports the progress of a long scan at fixed percentage steps
/// </summary>
/// <remarks>
/// The scans this reports on run to millions of iterations and every report crosses to the UI thread, so stepping keeps the number of
/// crossings bounded however large the backup is.
///
/// The message stays the same for the whole scan - only the percentage moves - so a consumer showing one line per stage gets one line for
/// the scan rather than one per step.
/// </remarks>
internal sealed class PercentageReporter(IProgress<ProgressDetail>? progress, string message, int step = 2)
{
    private int reported = -1;

    public void Report(long position, long total)
    {
        if (progress is null || total <= 0)
        {
            return;
        }

        var percentage = (int)(position * 100 / total) / step * step;

        if (percentage <= reported)
        {
            return;
        }

        reported = percentage;

        progress.Report(new ProgressDetail(message, percentage));
    }
}
