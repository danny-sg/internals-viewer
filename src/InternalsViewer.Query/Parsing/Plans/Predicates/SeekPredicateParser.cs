using System.Collections.Immutable;
using System.Xml.Linq;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Builds seek bounds from the SeekKeys of a showplan seek predicate
/// </summary>
/// <remarks>
/// A seek key has an optional Prefix of equality columns followed by an optional range on the next column. The prefix columns bound both
/// ends of the range, so a seek on a composite index with an equality prefix and a trailing range produces start and end keys sharing
/// their leading values.
/// </remarks>
public sealed class SeekPredicateParser(ColumnOrdinalResolver? resolveOrdinal = null,
                                        ParameterValueResolver? resolveParameter = null)
{
    private ScalarOperatorParser Expressions { get; } = new(resolveOrdinal, resolveParameter);

    private readonly List<CorrelatedSeekColumn> _correlatedColumns = [];

    public IReadOnlyList<CorrelatedSeekColumn> CorrelatedColumns => _correlatedColumns;

    /// <summary>
    /// Parses every seek key held by a SeekPredicates element
    /// </summary>
    /// <remarks>
    /// A seek can carry more than one key, which is how an IN list or an OR of equality tests is executed as several seeks against the
    /// same index.
    /// </remarks>
    public ImmutableArray<SeekBounds> ParseSeekPredicates(XElement? seekPredicates)
    {
        if (seekPredicates is null)
        {
            return [];
        }

        var seekKeys = seekPredicates.Descendants()
                                     .Where(e => e.Name.LocalName == ShowplanNames.SeekKeys)
                                     .Select(ParseSeekKeys)
                                     .Where(b => b is not null)
                                     .Select(b => b!)
                                     .ToImmutableArray();

        return seekKeys;
    }

    /// <summary>
    /// Parses a single SeekKeys element into the range it describes
    /// </summary>
    public SeekBounds? ParseSeekKeys(XElement seekKeys)
    {
        var prefix = ReadKeyValues(GetChild(seekKeys, ShowplanNames.Prefix));

        var startRange = GetChild(seekKeys, ShowplanNames.StartRange);
        var endRange = GetChild(seekKeys, ShowplanNames.EndRange);

        if (GetScanType(startRange) is "LE" or "LT" || GetScanType(endRange) is "GE" or "GT")
        {
            (startRange, endRange) = (endRange, startRange);
        }

        var startValues = ReadKeyValues(startRange);
        var endValues = ReadKeyValues(endRange);

        if (prefix.IsEmpty && startValues.IsEmpty && endValues.IsEmpty)
        {
            return null;
        }

        var startValue = prefix.AddRange(startValues);
        var endValue = prefix.AddRange(endValues);

        var isStartInclusive = startRange is null ||
                             ShowplanOperators.IsInclusiveBoundary(GetScanType(startRange));

        var isEndInclusive = endRange is null ||
                           ShowplanOperators.IsInclusiveBoundary(GetScanType(endRange));

        return new SeekBounds
        {
            StartValue = startValue.IsEmpty ? AccessKey.Unbounded : new AccessKey(startValue),
            IsStartInclusive = isStartInclusive,
            EndValue = endValue.IsEmpty ? AccessKey.Unbounded : new AccessKey(endValue),
            IsEndInclusive = isEndInclusive,
            CompareWidth = Math.Max(startValue.Length, endValue.Length)
        };
    }

    /// <summary>
    /// Reads the constant values a prefix or range boundary compares against
    /// </summary>
    /// <remarks>
    /// A boundary that compares against anything other than a constant, such as a correlated column from the outer side of a nested loops
    /// join, cannot be evaluated without running the plan, so the boundary is treated as absent.
    /// </remarks>
    private ImmutableArray<AccessValue> ReadKeyValues(XElement? boundary)
    {
        if (boundary is null)
        {
            return [];
        }

        var columnNames = GetChild(boundary, ShowplanNames.RangeColumns)
                                  ?.Elements()
                                  .Where(e => e.Name.LocalName == ShowplanNames.ColumnReference)
                                  .Select(e => e.Attribute(ShowplanNames.Column)?.Value)
                                  .ToList();

        var expressions = boundary.Descendants()
                                  .Where(e => e.Name.LocalName == ShowplanNames.RangeExpressions)
                                  .SelectMany(e => e.Elements())
                                  .Where(e => e.Name.LocalName == ShowplanNames.ScalarOperator)
                                  .ToList();

        var values = ImmutableArray.CreateBuilder<AccessValue>();

        for (var index = 0; index < expressions.Count; index++)
        {
            var parsed = Expressions.Parse(expressions[index]);

            if (parsed is not AccessExpression.Constant constant)
            {
                if (parsed is AccessExpression.Column
                    && Expressions.ParseColumnReference(expressions[index]) is { } outerColumn)
                {
                    var seekColumn = columnNames is not null && index < columnNames.Count ? columnNames[index] : null;

                    _correlatedColumns.Add(new CorrelatedSeekColumn(TrimName(seekColumn),
                                                                    TrimName(outerColumn.Table),
                                                                    TrimName(outerColumn.Column)));
                }

                return [];
            }

            var columnName = columnNames is not null && index < columnNames.Count ? columnNames[index] : null;

            values.Add(constant.Value.WithColumnName(columnName));
        }

        return values.ToImmutable();
    }

    private static string TrimName(string? name)
    {
        return name?.Trim('[', ']') ?? string.Empty;
    }

    private static string? GetScanType(XElement? boundary)
    {
        return boundary?.Attribute(ShowplanNames.ScanType)?.Value;
    }

    private static XElement? GetChild(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
    }
}
