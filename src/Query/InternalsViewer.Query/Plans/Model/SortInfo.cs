namespace InternalsViewer.Query.Plans.Model;

public sealed record SortInfo(bool Distinct, long? TopRows, bool WithTies);
