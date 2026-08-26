using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.AccessPaths.Predicates;

public sealed class CompressedDataFilter
{
    private CompressedDataFilter((ComparisonOperator Operator, decimal DataId)[] tests,
                                 bool hasNulls,
                                 long nullValue)
    {
        Tests = tests;

        HasNulls = hasNulls;

        NullValue = nullValue;
    }

    private (ComparisonOperator Operator, decimal DataId)[] Tests { get; }

    private bool HasNulls { get; }

    private long NullValue { get; }

    public static CompressedDataFilter? Create(AccessPredicate? predicate, ColumnSegment segment)
    {
        if (predicate is null || segment.Column?.Structure is not { } structure)
        {
            return null;
        }

        if (segment.PrimaryDictionaryId >= 0 || segment.SecondaryDictionaryId >= 0)
        {
            return null;
        }

        var magnitude = segment.Magnitude > 0 && Math.Abs(segment.Magnitude - 1) > double.Epsilon
                        ? (decimal)segment.Magnitude
                        : 1m;

        var tests = new List<(ComparisonOperator, decimal)>();

        foreach (var comparison in Conjunctions(predicate))
        {
            if (!TryResolve(comparison, structure.ColumnName, out var comparisonOperator, out var literal))
            {
                continue;
            }

            tests.Add((comparisonOperator, (literal / magnitude) - segment.BaseId));
        }

        return tests.Count == 0 ? null : new CompressedDataFilter([.. tests], segment.HasNulls, segment.NullValue ?? 0);
    }

    public bool Matches(long dataId)
    {
        if (HasNulls && dataId == NullValue)
        {
            return false;
        }

        for (var i = 0; i < Tests.Length; i++)
        {
            if (!Satisfies(Tests[i].Operator, dataId, Tests[i].DataId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Satisfies(ComparisonOperator comparisonOperator, decimal dataId, decimal target)
        => comparisonOperator switch
        {
            ComparisonOperator.Equal => dataId == target,
            ComparisonOperator.NotEqual => dataId != target,
            ComparisonOperator.LessThan => dataId < target,
            ComparisonOperator.LessThanOrEqual => dataId <= target,
            ComparisonOperator.GreaterThan => dataId > target,
            ComparisonOperator.GreaterThanOrEqual => dataId >= target,
            _ => true
        };

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

    private static ComparisonOperator Flip(ComparisonOperator comparisonOperator)
        => comparisonOperator switch
        {
            ComparisonOperator.LessThan => ComparisonOperator.GreaterThan,
            ComparisonOperator.LessThanOrEqual => ComparisonOperator.GreaterThanOrEqual,
            ComparisonOperator.GreaterThan => ComparisonOperator.LessThan,
            ComparisonOperator.GreaterThanOrEqual => ComparisonOperator.LessThanOrEqual,
            _ => comparisonOperator
        };
}
