namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TracePositions
{
    public static TracePosition At(ulong sequence, ulong steps = 0) => new(sequence, steps);
}
