namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOutputRow(int Ordinal,
                                    TraceRowReturn Return,
                                    IReadOnlyList<TraceOutputStep> Steps,
                                    IReadOnlyList<TraceOutputRowSource> Sources);
