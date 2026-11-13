using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;
using Windows.UI;

namespace InternalsViewer.UI.App.Helpers;

public static class DataPathText
{
    private static readonly (ColumnUseType Use, string Name)[] UseNames =
    [
        (ColumnUseType.BuildKey, "Build Key"),
        (ColumnUseType.ProbeKey, "Probe Key"),
        (ColumnUseType.Predicate, "Predicate"),
        (ColumnUseType.Group, "Group By"),
        (ColumnUseType.Order, "Order By")
    ];

    public static Color UseColour { get; } = Color.FromArgb(255, 0x80, 0x80, 0x80);

    public static string GetUses(ColumnUseType uses) => string.Join(", ", UseNames.Where(u => (uses & u.Use) != 0).Select(u => u.Name));

    public static IEnumerable<string> GetDefinitions(PlanNode node)
        => node.DefinedValues.Select(d => $"{string.Join(", ", d.Columns.Select(c => c.Column))} = {GetText(d)}");

    public static PredicateText GetExpression(DefinedValueInfo value)
        => value.ParsedExpression is { } parsed
            ? PredicateText.From(parsed)
            : new PredicateText([new PredicateToken(PredicateTokenType.Unknown, value.Expression ?? string.Empty)]);

    public static string GetText(DefinedValueInfo value) => GetExpression(value).ToString();

    public static string GetObjectName(PlanNode node)
        => string.IsNullOrEmpty(node.Table)
            ? string.Empty
            : string.Join(".", new[] { node.Schema, node.Table, node.Index }.Where(p => !string.IsNullOrEmpty(p)));
}
