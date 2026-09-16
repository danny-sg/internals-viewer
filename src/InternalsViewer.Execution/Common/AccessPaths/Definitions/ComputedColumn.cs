using System.Data;
using InternalsViewer.Execution.Common.AccessPaths.Predicates;

namespace InternalsViewer.Execution.Common.AccessPaths.Definitions;

public sealed record ComputedColumn(string Name, AccessExpression Expression)
{
    public SqlDbType? DataType { get; init; }

    public string Text { get; init; } = string.Empty;
}
