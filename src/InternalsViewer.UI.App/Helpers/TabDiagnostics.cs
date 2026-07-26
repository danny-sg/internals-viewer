using System.Diagnostics;

namespace InternalsViewer.UI.App.Helpers;

/// <summary>
/// Temporary instrumentation for diagnosing page tab switch cost
/// </summary>
/// <remarks>
/// Answers whether TabView detaches and reattaches tab content on every selection change. If Loaded fires once per
/// control the content stays in the tree and the cost is re-measure. If it fires on every switch the subtree is being
/// torn down and rebuilt, and per-Loaded work like AllocationControl.Refresh runs again each time. Delete once the
/// question is settled.
/// </remarks>
internal static class TabDiagnostics
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    [Conditional("DEBUG")]
    public static void Log(string source, string message)
    {
        Debug.WriteLine($"[tab-diag] {Clock.Elapsed.TotalMilliseconds,10:F1} ms  {source,-20} {message}");
    }

    public static long Start() => Stopwatch.GetTimestamp();

    [Conditional("DEBUG")]
    public static void LogElapsed(string source, string message, long startTimestamp)
    {
        Log(source, $"{message} took {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F1} ms");
    }
}
