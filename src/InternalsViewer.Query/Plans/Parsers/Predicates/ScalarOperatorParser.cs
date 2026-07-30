using System.Collections.Immutable;
using System.Xml.Linq;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Supplies the compiled value of a plan parameter
/// </summary>
/// <remarks>
/// An auto parameterized plan replaces literals with parameters, so the value a seek compares against is held in the plan's ParameterList
/// rather than at the point of use.
/// </remarks>
public delegate AccessValue? ParameterValueResolver(string parameterName);

/// <summary>
/// Builds access path expressions from a showplan ScalarOperator tree
/// </summary>
public sealed class ScalarOperatorParser(ColumnOrdinalResolver? resolveOrdinal = null,
                                         ParameterValueResolver? resolveParameter = null,
                                         Func<XElement?, AccessPredicate?>? resolvePredicate = null)
{
    private ColumnOrdinalResolver ResolveOrdinal { get; } = resolveOrdinal ?? (_ => null);

    private ParameterValueResolver ResolveParameter { get; } = resolveParameter ?? (_ => null);

    private Func<XElement?, AccessPredicate?> ResolvePredicate { get; } = resolvePredicate ?? (_ => null);

    /// <summary>
    /// Parses a scalar expression
    /// </summary>
    /// <remarks>
    /// An unrepresentable expression is not an error. A predicate containing one is treated as unknown rather than being dropped, so the
    /// caller can still show that a predicate exists.
    /// </remarks>
    public AccessExpression? Parse(XElement? scalarOperator)
    {
        var content = GetContent(scalarOperator);

        if (content is null)
        {
            return null;
        }

        return content.Name.LocalName switch
        {
            ShowplanNames.Const => ParseConstant(content),
            ShowplanNames.Identifier => ParseIdentifier(content),
            ShowplanNames.Arithmetic => ParseArithmetic(content),
            ShowplanNames.Intrinsic => ParseFunction(content),
            ShowplanNames.If => ParseIf(content),
            ShowplanNames.Aggregate => ParseAggregate(content),
            _ => null
        };
    }

    private AccessExpression? ParseAggregate(XElement element)
    {
        var name = element.Attribute(ShowplanNames.AggType)?.Value;

        if (name is null)
        {
            return null;
        }

        var isDistinct = element.Attribute(ShowplanNames.Distinct)?.Value is "1" or "true";

        var operands = element.Elements()
                              .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                              .ToList();

        var arguments = ImmutableArray.CreateBuilder<AccessExpression>(operands.Count);

        foreach (var operand in operands)
        {
            var argument = Parse(operand);

            if (argument is null)
            {
                return null;
            }

            arguments.Add(argument);
        }

        return new AccessExpression.Aggregate(name.ToUpperInvariant(), isDistinct, arguments.MoveToImmutable());
    }

    private AccessExpression? ParseFunction(XElement element)
    {
        var name = element.Attribute(ShowplanNames.FunctionName)?.Value;

        if (name is null)
        {
            return null;
        }

        var operands = element.Elements()
                              .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                              .ToList();

        if (!IntrinsicFunctions.IsSupported(name, operands.Count))
        {
            return null;
        }

        var arguments = ImmutableArray.CreateBuilder<AccessExpression>(operands.Count);

        foreach (var operand in operands)
        {
            var argument = Parse(operand);

            if (argument is null)
            {
                return null;
            }

            arguments.Add(argument);
        }

        return new AccessExpression.Function(name.ToUpperInvariant(), arguments.MoveToImmutable());
    }

    private AccessExpression? ParseIf(XElement element)
    {
        var condition = ResolvePredicate(GetChildScalar(element, ShowplanNames.Condition));

        var then = Parse(GetChildScalar(element, ShowplanNames.Then));
        var otherwise = Parse(GetChildScalar(element, ShowplanNames.Else));

        if (condition is null || then is null || otherwise is null)
        {
            return null;
        }

        return new AccessExpression.Conditional(condition, then, otherwise);
    }

    private static XElement? GetChildScalar(XElement element, string name)
    {
        return element.Elements()
                      .FirstOrDefault(e => e.Name.LocalName == name)?
                      .Elements()
                      .FirstOrDefault(e => e.Name.LocalName == ShowplanNames.ScalarOperator);
    }

    private AccessExpression? ParseArithmetic(XElement element)
    {
        var operation = element.Attribute(ShowplanNames.Operation)?.Value switch
        {
            "ADD" => ArithmeticOperator.Add,
            "SUB" => ArithmeticOperator.Subtract,
            "MULT" => ArithmeticOperator.Multiply,
            "DIV" => ArithmeticOperator.Divide,
            "MOD" => ArithmeticOperator.Modulo,
            _ => (ArithmeticOperator?)null
        };

        if (operation is null)
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

        var left = Parse(operands[0]);
        var right = Parse(operands[1]);

        if (left is null || right is null)
        {
            return null;
        }

        return new AccessExpression.Arithmetic(operation.Value, left, right);
    }

    /// <summary>
    /// Gets the column a scalar expression refers to, ignoring any ordinal mapping
    /// </summary>
    public ColumnReference? ParseColumnReference(XElement? scalarOperator)
    {
        var content = GetContent(scalarOperator);

        var reference = content?.Name.LocalName == ShowplanNames.ColumnReference
            ? content
            : content?.Elements().FirstOrDefault(e => e.Name.LocalName == ShowplanNames.ColumnReference);

        return reference is null ? null : ReadColumnReference(reference);
    }

    /// <summary>
    /// Gets the element carrying the meaning of a scalar operator
    /// </summary>
    /// <remarks>
    /// A ScalarOperator wraps a single child describing what it does, but predicates nest scalar operators inside one another, so the
    /// wrapper is unwrapped until the described element is reached.
    ///
    /// A Convert is unwrapped too. An implicit conversion is applied by the engine to make types match and does not change which value is
    /// being compared, so the operand underneath is what the access path needs.
    /// </remarks>
    internal static XElement? GetContent(XElement? element)
    {
        while (element?.Name.LocalName is ShowplanNames.ScalarOperator or ShowplanNames.Convert)
        {
            element = element.Elements().FirstOrDefault();
        }

        return element;
    }

    private static AccessExpression ParseConstant(XElement element)
    {
        var literal = element.Attribute(ShowplanNames.ConstValue)?.Value;

        return new AccessExpression.Constant(ConstValueParser.Parse(literal));
    }

    private AccessExpression? ParseIdentifier(XElement element)
    {
        var reference = element.Elements()
                               .FirstOrDefault(e => e.Name.LocalName == ShowplanNames.ColumnReference);

        if (reference is null)
        {
            return null;
        }

        var definition = reference.Elements()
                                  .FirstOrDefault(e => e.Name.LocalName == ShowplanNames.ScalarOperator);

        if (definition is not null)
        {
            return Parse(definition);
        }

        var column = ReadColumnReference(reference);

        // A reference with no table naming it and a leading @ is a parameter, not a column
        if (column.Column.StartsWith('@') && string.IsNullOrEmpty(column.Table))
        {
            var parameter = ResolveParameter(column.Column);

            return parameter is null ? null : new AccessExpression.Constant(parameter.Value);
        }

        var ordinal = ResolveOrdinal(column);

        if (ordinal is null && string.IsNullOrEmpty(column.Table))
        {
            return null;
        }

        return new AccessExpression.Column(ordinal ?? -1, column.Column);
    }

    private static ColumnReference ReadColumnReference(XElement element)
    {
        return new ColumnReference
        {
            Database = element.Attribute("Database")?.Value ?? string.Empty,
            Schema = element.Attribute("Schema")?.Value ?? string.Empty,
            Table = element.Attribute("Table")?.Value ?? string.Empty,
            Column = element.Attribute(ShowplanNames.Column)?.Value ?? string.Empty
        };
    }
}
