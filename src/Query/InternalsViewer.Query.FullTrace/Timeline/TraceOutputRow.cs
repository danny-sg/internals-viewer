namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceOutputRow
{
    internal TraceOutputRow(int ordinal, TraceRowReturn rowReturn, IReadOnlyList<TraceOutputStep> steps, TraceOutputLineage lineage)
    {
        Ordinal = ordinal;

        Return = rowReturn;

        Steps = steps;

        Lineage = lineage;
    }

    public int Ordinal { get; }

    public TraceRowReturn Return { get; }

    public IReadOnlyList<TraceOutputStep> Steps { get; }

    public IReadOnlyList<TraceOutputRowSource> Sources => field ??= Lineage.GetSources(Return);

    private TraceOutputLineage Lineage { get; }
}
