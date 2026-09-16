namespace InternalsViewer.Execution.Common.AccessPaths.Windowing;

public sealed record RankingColumn(string Column, RankingFunction Function)
{
    public string ToText() => $"{Function.ToDisplayName()}()";
}
