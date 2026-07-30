using System.Collections.Immutable;
using System.Text.RegularExpressions;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;

namespace InternalsViewer.Query.Plans.Model;

public sealed class ExpressionCatalog
{
    private static readonly Regex ReferencePattern = new(@"\[(?<name>[A-Za-z_][A-Za-z0-9_]*\d{3,})\]", RegexOptions.Compiled);

    private static readonly Regex MappingPattern =
        new(@"^(?:\[(?<name>[A-Za-z_][A-Za-z0-9_]*\d{3,})\]" +
            @"|CONVERT_IMPLICIT\([A-Za-z0-9_]+(?:\(\s*\d+(?:\s*,\s*\d+)?\s*\))?\s*,\s*\[(?<name>[A-Za-z_][A-Za-z0-9_]*\d{3,})\]\s*,\s*\d+\))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, ExpressionDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public static ExpressionCatalog Build(ExecutionPlan plan)
    {
        var catalog = new ExpressionCatalog();

        foreach (var root in plan.Root)
        {
            Collect(root, catalog);
        }

        return catalog;
    }

    public static void Populate(IReadOnlyList<ExecutionPlan> plans, IReadOnlyList<QueryResultSet> resultSets)
    {
        var outputs = new List<(ExpressionCatalog Catalog, PlanNode Node)>();

        foreach (var plan in plans.Where(p => !p.IsInternalPlan))
        {
            plan.Expressions = Build(plan);

            foreach (var root in plan.Root)
            {
                if (FindOutputNode(root) is { } outputNode)
                {
                    outputs.Add((plan.Expressions, outputNode));
                }
            }
        }

        if (outputs.Count != resultSets.Count)
        {
            return;
        }

        for (var index = 0; index < outputs.Count; index++)
        {
            var (catalog, node) = outputs[index];

            var columns = resultSets[index].Columns;

            if (node.OutputColumns.Count != columns.Count)
            {
                continue;
            }

            for (var column = 0; column < columns.Count; column++)
            {
                var definition = catalog.Find(node.OutputColumns[column].Column);

                if (definition is { Alias: null } && !string.IsNullOrWhiteSpace(columns[column].Name))
                {
                    definition.Alias = columns[column].Name;
                }
            }
        }

        foreach (var catalog in outputs.Select(o => o.Catalog).Distinct())
        {
            catalog.PropagateAliases();
        }
    }

    private void PropagateAliases()
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.Alias is null)
            {
                continue;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { definition.Name };

            var target = definition.MappedTo;

            while (target is not null
                   && visited.Add(target)
                   && _definitions.TryGetValue(target, out var mapped))
            {
                mapped.Alias ??= definition.Alias;

                target = mapped.MappedTo;
            }
        }
    }

    private static string? GetMappingTarget(string? expression, string name)
    {
        if (expression is null)
        {
            return null;
        }

        var match = MappingPattern.Match(expression.Trim());

        if (!match.Success)
        {
            return null;
        }

        var target = match.Groups["name"].Value;

        return target.Equals(name, StringComparison.OrdinalIgnoreCase) ? null : target;
    }

    public ExpressionDefinition? Find(string name)
    {
        return _definitions.GetValueOrDefault(name.Trim('[', ']'));
    }

    public string GetDisplayText(string name)
    {
        if (Find(name) is not { } definition)
        {
            return name;
        }

        if (!string.IsNullOrEmpty(definition.Alias))
        {
            return definition.Alias;
        }

        return GetExpandedText(definition) is { Length: <= 60 } expanded ? expanded : name;
    }

    public string? GetExpandedText(ExpressionDefinition definition)
    {
        return GetExpandedText(definition, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { definition.Name });
    }

    private string? GetExpandedText(ExpressionDefinition definition, HashSet<string> visited)
    {
        if (definition.ParsedExpression is { } parsed)
        {
            return PredicateWriter.ToText(ExpandTokens(PredicateWriter.Write(parsed), visited));
        }

        if (definition.Expression is { } text)
        {
            return ExpandText(text, visited);
        }

        return null;
    }

    public PredicateText Expand(PredicateText text)
    {
        return text.IsEmpty ? text : new PredicateText(ExpandTokens(text.Tokens));
    }

    public ImmutableArray<PredicateToken> ExpandTokens(ImmutableArray<PredicateToken> tokens)
    {
        return ExpandTokens(tokens, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static void Collect(PlanNode node, ExpressionCatalog catalog)
    {
        foreach (var definedValue in node.DefinedValues)
        {
            if (definedValue.Columns.Count != 1 || (definedValue.Expression is null && definedValue.ParsedExpression is null))
            {
                continue;
            }

            var name = definedValue.Columns[0].Column.Trim('[', ']');

            catalog._definitions.TryAdd(name, new ExpressionDefinition
            {
                Name = name,
                NodeId = node.NodeId,
                Expression = definedValue.Expression,
                ParsedExpression = definedValue.ParsedExpression,
                MappedTo = GetMappingTarget(definedValue.Expression, name)
            });
        }

        foreach (var child in node.Children)
        {
            Collect(child, catalog);
        }
    }

    private static PlanNode? FindOutputNode(PlanNode root)
    {
        var current = root;

        while (current is not null && current.OutputColumns.Count == 0)
        {
            current = current.Children.FirstOrDefault();
        }

        return current;
    }

    private ImmutableArray<PredicateToken> ExpandTokens(ImmutableArray<PredicateToken> tokens, HashSet<string> visited)
    {
        if (tokens.IsDefaultOrEmpty)
        {
            return tokens;
        }

        var builder = ImmutableArray.CreateBuilder<PredicateToken>();

        foreach (var token in tokens)
        {
            if (token.Type != PredicateTokenType.Column || Find(token.Text) is not { } definition || !visited.Add(definition.Name))
            {
                builder.Add(token);

                continue;
            }

            builder.AddRange(GetDisplayTokens(definition, visited));

            visited.Remove(definition.Name);
        }

        return builder.ToImmutable();
    }

    private IEnumerable<PredicateToken> GetDisplayTokens(ExpressionDefinition definition, HashSet<string> visited)
    {
        if (!string.IsNullOrEmpty(definition.Alias))
        {
            return [new PredicateToken(PredicateTokenType.Column, definition.Alias, definition.Name)];
        }

        if (definition.ParsedExpression is { } parsed)
        {
            return ExpandTokens(PredicateWriter.Write(parsed), visited);
        }

        if (definition.Expression is { } text)
        {
            return [new PredicateToken(PredicateTokenType.Column,
                                       definition.Name,
                                       ExpandText(text, new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase)))];
        }

        return [new PredicateToken(PredicateTokenType.Column, definition.Name)];
    }

    private string ExpandText(string text, HashSet<string> visited)
    {
        return ReferencePattern.Replace(text, match =>
        {
            var name = match.Groups["name"].Value;

            if (!_definitions.TryGetValue(name, out var definition) || !visited.Add(name))
            {
                return match.Value;
            }

            var expanded = !string.IsNullOrEmpty(definition.Alias)
                ? definition.Alias
                : definition.Expression is { } expressionText
                    ? ExpandText(expressionText, visited)
                    : match.Value;

            visited.Remove(name);

            return expanded;
        });
    }
}
