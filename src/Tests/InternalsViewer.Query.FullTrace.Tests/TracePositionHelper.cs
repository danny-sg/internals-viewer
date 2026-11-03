namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TracePositionHelper
{
    public static TracePosition At(ulong sequence, ulong steps = 0) => new(sequence, steps);
}
