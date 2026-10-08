using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Model;

public sealed class ExpressionDefinition
{
    public required string Name { get; init; }

    public required int NodeId { get; init; }

    public string? Expression { get; init; }

    public AccessExpression? ParsedExpression { get; init; }

    public string? Alias { get; internal set; }

    public string? MappedTo { get; internal set; }
}