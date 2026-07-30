namespace InternalsViewer.Query.Parsing.Plans;

public sealed record CorrelatedSeekColumn(string Column, string OuterReference);
