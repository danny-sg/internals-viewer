using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Model;

public sealed record DefinedValueInfo
{
    public List<ColumnReference> Columns { get; init; } = [];

    public string? Expression { get; init; }

    public AccessExpression? ParsedExpression { get; init; }
}
