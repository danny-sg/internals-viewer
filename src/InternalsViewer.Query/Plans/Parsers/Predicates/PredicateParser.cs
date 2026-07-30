using System.Collections.Immutable;
using System.Xml.Linq;
using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Builds access path predicates from a showplan ScalarOperator tree
/// </summary>
/// <remarks>
/// Returns null when a predicate cannot be represented, rather than substituting a predicate that
/// would be wrong. A residual that silently became true would let more rows through than the plan
/// describes, and one that silently became false would let fewer through, so an unrepresentable
/// predicate is reported as absent and the caller decides what to do.
/// </remarks>
public sealed class PredicateParser(ColumnOrdinalResolver? resolveOrdinal = null,
                                    ParameterValueResolver? resolveParameter = null)
{
    private ScalarOperatorParser? _expressions;

    private ScalarOperatorParser Expressions => _expressions ??= new(resolveOrdinal, resolveParameter, Parse);

    /// <summary>
    /// Parses a scalar operator known to yield a value
    /// </summary>
    public AccessExpression? ParseExpression(XElement? scalarOperator)
    {
        return Expressions.Parse(scalarOperator);
    }

    /// <summary>
    /// Parses the predicate held by a Predicate element
    /// </summary>
    public AccessPredicate? ParsePredicateElement(XElement? predicateElement)
    {
        var scalarOperator = predicateElement?
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == ShowplanNames.ScalarOperator);

        return Parse(scalarOperator);
    }

    /// <summary>
    /// Parses a scalar operator known to yield a boolean result
    /// </summary>
    public AccessPredicate? Parse(XElement? scalarOperator)
    {
        var content = ScalarOperatorParser.GetContent(scalarOperator);

        if (content is null)
        {
            return null;
        }

        return content.Name.LocalName switch
        {
            ShowplanNames.Compare => ParseCompare(content),
            ShowplanNames.Logical => ParseLogical(content),
            ShowplanNames.Intrinsic => ParseIntrinsic(content),
            _ => null
        };
    }

    private AccessPredicate? ParseCompare(XElement element)
    {
        var comparison = ShowplanOperators.ParseComparison(element.Attribute(ShowplanNames.CompareOp)?.Value);

        if (comparison is null)
        {
            return null;
        }

        var operands = element.Elements()
                              .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                              .ToList();

        if (operands.Count != 2)
        {
            return null;
        }

        var left = Expressions.Parse(operands[0]);
        var right = Expressions.Parse(operands[1]);

        if (left is null || right is null)
        {
            return null;
        }

        return new AccessPredicate.Comparison(left, comparison.Value, right);
    }

    private AccessPredicate? ParseLogical(XElement element)
    {
        var operation = element.Attribute(ShowplanNames.Operation)?.Value;

        var operandElements = element.Elements()
                                     .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                                     .ToList();

        if (operandElements.Count == 0)
        {
            return null;
        }

        // IS NULL takes a scalar operand rather than a nested predicate
        if (operation is "IS NULL" or "IS NOT NULL")
        {
            if (operandElements.Count != 1)
            {
                return null;
            }

            var expression = Expressions.Parse(operandElements[0]);

            if (expression is null)
            {
                return null;
            }

            AccessPredicate isNull = new AccessPredicate.IsNull(expression);

            return operation == "IS NULL" ? isNull : new AccessPredicate.Not(isNull);
        }

        var operands = operandElements.Select(Parse).ToList();

        if (operands.Any(p => p is null))
        {
            return null;
        }

        var predicates = operands.Select(p => p!).ToImmutableArray();

        return operation switch
        {
            "AND" => predicates.Length == 1 ? predicates[0] : new AccessPredicate.And(predicates),
            "OR" => predicates.Length == 1 ? predicates[0] : new AccessPredicate.Or(predicates),
            "NOT" when predicates.Length == 1 => new AccessPredicate.Not(predicates[0]),
            _ => null
        };
    }

    /// <summary>
    /// Parses the intrinsic functions that appear where a predicate is expected
    /// </summary>
    private AccessPredicate? ParseIntrinsic(XElement element)
    {
        var function = element.Attribute(ShowplanNames.FunctionName)?.Value;

        if (!string.Equals(function, "like", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var arguments = element.Descendants()
                               .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                               .Take(2)
                               .ToList();

        if (arguments.Count != 2)
        {
            return null;
        }

        var target = Expressions.Parse(arguments[0]);

        var pattern = ScalarOperatorParser.GetContent(arguments[1]);

        if (target is null || pattern?.Name.LocalName != ShowplanNames.Const)
        {
            return null;
        }

        var literal = pattern.Attribute(ShowplanNames.ConstValue)?.Value ?? string.Empty;

        return new AccessPredicate.Like(target, Unquote(literal));
    }

    private static string Unquote(string literal)
    {
        var text = literal.Trim();

        if (text.StartsWith('N'))
        {
            text = text[1..];
        }

        if (text.Length >= 2 && text.StartsWith('\'') && text.EndsWith('\''))
        {
            text = text[1..^1].Replace("''", "'");
        }

        return text;
    }
}
