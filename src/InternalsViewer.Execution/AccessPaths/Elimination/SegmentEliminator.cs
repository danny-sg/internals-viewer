using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.AccessPaths.Elimination;

public sealed class SegmentEliminator(AccessPredicate? predicate)
{
    private AccessPredicate? Predicate { get; } = predicate;

    public EliminationResult Evaluate(ColumnSegment segment)
    {
        if (Predicate is null || segment.Column?.Structure is not { } structure)
        {
            return EliminationResult.Kept;
        }

        if (segment.PrimaryDictionaryId >= 0 || segment.SecondaryDictionaryId >= 0)
        {
            return EliminationResult.Kept;
        }

        var (minimum, maximum) = ValueRange(segment);

        foreach (var comparison in Conjunctions(Predicate))
        {
            if (!TryResolve(comparison, structure.ColumnName, out var comparisonOperator, out var literal))
            {
                continue;
            }

            if (!CannotMatch(comparisonOperator, literal, minimum, maximum))
            {
                continue;
            }

            return EliminationResult.Eliminated($"{structure.ColumnName} {Symbol(comparisonOperator)} {literal:G29} "
                                                + $"outside {minimum:G29} to {maximum:G29}");
        }

        return EliminationResult.Kept;
    }

    private static IEnumerable<AccessPredicate.Comparison> Conjunctions(AccessPredicate predicate)
    {
        switch (predicate)
        {
            case AccessPredicate.Comparison comparison:
                yield return comparison;

                break;

            case AccessPredicate.And and:
                foreach (var inner in and.Predicates.SelectMany(Conjunctions))
                {
                    yield return inner;
                }

                break;
        }
    }

    private static bool TryResolve(AccessPredicate.Comparison comparison,
                                   string columnName,
                                   out ComparisonOperator comparisonOperator,
                                   out decimal literal)
    {
        comparisonOperator = comparison.Operator;

        literal = 0;

        if (IsColumn(comparison.Left, columnName) && comparison.Right is AccessExpression.Constant right)
        {
            return TryGetNumber(right.Value, out literal);
        }

        if (IsColumn(comparison.Right, columnName) && comparison.Left is AccessExpression.Constant left)
        {
            comparisonOperator = Flip(comparison.Operator);

            return TryGetNumber(left.Value, out literal);
        }

        return false;
    }

    private static bool IsColumn(AccessExpression expression, string columnName)
        => expression is AccessExpression.Column column
           && string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetNumber(AccessValue value, out decimal number)
    {
        switch (value.Type)
        {
            case AccessValueType.Integer:
                number = value.Numeric;
                return true;

            case AccessValueType.Real:
                number = (decimal)value.Real;
                return true;

            case AccessValueType.Decimal:
                number = value.ToDecimal();
                return true;

            default:
                number = 0;
                return false;
        }
    }

    private static (decimal Minimum, decimal Maximum) ValueRange(ColumnSegment segment)
        => segment.MinDataId <= segment.MaxDataId
            ? (segment.MinDataId, segment.MaxDataId)
            : (segment.MaxDataId, segment.MinDataId);

    private static bool CannotMatch(ComparisonOperator comparisonOperator, decimal literal, decimal minimum, decimal maximum)
        => comparisonOperator switch
        {
            ComparisonOperator.Equal => literal < minimum || literal > maximum,
            ComparisonOperator.LessThan => literal <= minimum,
            ComparisonOperator.LessThanOrEqual => literal < minimum,
            ComparisonOperator.GreaterThan => literal >= maximum,
            ComparisonOperator.GreaterThanOrEqual => literal > maximum,
            _ => false
        };

    private static ComparisonOperator Flip(ComparisonOperator comparisonOperator)
        => comparisonOperator switch
        {
            ComparisonOperator.LessThan => ComparisonOperator.GreaterThan,
            ComparisonOperator.LessThanOrEqual => ComparisonOperator.GreaterThanOrEqual,
            ComparisonOperator.GreaterThan => ComparisonOperator.LessThan,
            ComparisonOperator.GreaterThanOrEqual => ComparisonOperator.LessThanOrEqual,
            _ => comparisonOperator
        };

    private static string Symbol(ComparisonOperator comparisonOperator)
        => comparisonOperator switch
        {
            ComparisonOperator.Equal => "=",
            ComparisonOperator.NotEqual => "<>",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThan => ">",
            _ => ">="
        };
}
