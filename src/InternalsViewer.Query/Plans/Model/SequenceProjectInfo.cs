using InternalsViewer.Execution.AccessPaths.Windowing;

namespace InternalsViewer.Query.Plans.Model;

public sealed record SequenceProjectInfo
{
    public List<RankingColumn> Columns { get; init; } = [];

    public bool HasUntranslatedFunction { get; init; }
}
