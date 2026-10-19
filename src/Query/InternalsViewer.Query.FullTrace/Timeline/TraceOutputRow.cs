namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceOutputRow
{
    internal TraceOutputRow(int ordinal, TraceRowReturn rowReturn, IReadOnlyList<TraceOutputStep> steps)
    {
        Ordinal = ordinal;

        Return = rowReturn;

        Steps = steps;
    }

    public int Ordinal { get; }

    public TraceRowReturn Return { get; }

    public IReadOnlyList<TraceOutputStep> Steps { get; }
}
