using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.AccessPaths.Predicates;

public sealed class CompressedDataFilter
{
    private CompressedDataFilter((ComparisonOperator Operator, decimal DataId)[] tests,
                                 HashSet<long>? matchingIds,
                                 AccessPredicate.Comparison[] claimed,
                                 bool hasNulls,
                                 long nullValue)
    {
        Tests = tests;

        MatchingIds = matchingIds;

        Claimed = claimed;

        HasNulls = hasNulls;

        NullValue = nullValue;
    }

    public IReadOnlyList<AccessPredicate.Comparison> Claimed { get; }

    private (ComparisonOperator Operator, decimal DataId)[] Tests { get; }

    private HashSet<long>? MatchingIds { get; }

    private bool HasNulls { get; }

    private long NullValue { get; }

    public static CompressedDataFilter? Create(AccessPredicate? predicate, SegmentReader reader, EvaluationContext context)
    {
        var segment = reader.Segment;

        if (predicate is null || segment.Column?.Structure is not { } structure)
        {
            return null;
        }

        if (segment.PrimaryDictionaryId >= 0 || segment.SecondaryDictionaryId >= 0)
        {
            return CreateForDictionary(predicate, reader, structure, context);
        }

        var magnitude = segment.Magnitude > 0 && Math.Abs(segment.Magnitude - 1) > double.Epsilon
                        ? (decimal)segment.Magnitude
                        : 1m;

        var tests = new List<(ComparisonOperator, decimal)>();

        var claimed = new List<AccessPredicate.Comparison>();

        foreach (var comparison in Conjunctions(predicate))
        {
            if (!TryResolve(comparison, structure.ColumnName, out var comparisonOperator, out var literal))
            {
                continue;
            }

            tests.Add((comparisonOperator, (literal / magnitude) - segment.BaseId));

            claimed.Add(comparison);
        }

        return tests.Count == 0
               ? null
               : new CompressedDataFilter([.. tests], null, [.. claimed], segment.HasNulls, segment.NullValue ?? 0);
    }

    public bool Matches(long dataId)
    {
        if (HasNulls && dataId == NullValue)
        {
            return false;
        }

        if (MatchingIds is { } ids)
        {
            return ids.Contains(dataId);
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

    public static HashSet<long>? MatchingDictionaryIds(AccessPredicate? predicate,
                                                       SegmentReader reader,
                                                       EvaluationContext context)
    {
        if (predicate is null || reader.Segment.Column?.Structure is not { } structure)
        {
            return null;
        }

        var comparisons = Conjunctions(predicate)
                          .Where(c => References(c, structure.ColumnName))
                          .ToList();

        if (comparisons.Count == 0)
        {
            return null;
        }

        var matching = new HashSet<long>();

        foreach (var dataId in reader.DictionaryDataIds)
        {
            var value = AccessValueFactory.FromObject(structure.DataType, reader.GetValueForDataId(dataId));

            var source = new ColumnValueSource(structure.ColumnName, value);

            if (comparisons.TrueForAll(c => PredicateEvaluator.Evaluate(c, source, context) == true))
            {
                matching.Add(dataId);
            }
        }

        return matching;
    }

    public static bool IsPureConjunction(AccessPredicate predicate)
        => predicate switch
        {
            AccessPredicate.Comparison => true,
            AccessPredicate.And and => and.Predicates.All(IsPureConjunction),
            _ => false
        };

    public static IEnumerable<AccessPredicate.Comparison> Conjunctions(AccessPredicate predicate)
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

    private static CompressedDataFilter? CreateForDictionary(AccessPredicate predicate,
                                                             SegmentReader reader,
                                                             ColumnStructure structure,
                                                             EvaluationContext context)
    {
        if (MatchingDictionaryIds(predicate, reader, context) is not { } matching)
        {
            return null;
        }

        var claimed = Conjunctions(predicate)
                      .Where(c => References(c, structure.ColumnName))
                      .ToList();

        var segment = reader.Segment;

        return new CompressedDataFilter([], matching, [.. claimed], segment.HasNulls, segment.NullValue ?? 0);
    }

    private static bool References(AccessPredicate.Comparison comparison, string columnName)
        => (IsColumn(comparison.Left, columnName) && comparison.Right is AccessExpression.Constant)
           || (IsColumn(comparison.Right, columnName) && comparison.Left is AccessExpression.Constant);

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

    private sealed class ColumnValueSource(string columnName, AccessValue value) : IRowValueSource
    {
        public AccessValue GetValue(int ordinal, string? name = null)
            => name is null || string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase)
                ? value
                : AccessValue.Null;
    }
}
