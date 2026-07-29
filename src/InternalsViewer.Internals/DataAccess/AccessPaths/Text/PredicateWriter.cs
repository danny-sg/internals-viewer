using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Text;

/// <summary>
/// Turns predicates and seek bounds into a sequence of formatted tokens
/// </summary>
public static class PredicateWriter
{
    /// <summary>
    /// Writes a predicate as tokens
    /// </summary>
    public static ImmutableArray<PredicateToken> Write(AccessPredicate predicate)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WritePredicate(tokens, predicate, false);

        return tokens.ToImmutable();
    }

    /// <summary>
    /// Writes seek bounds as the range condition they represent
    /// </summary>
    /// <remarks>
    /// Bounds are written against the column name each boundary value carries, falling back to a positional name when a value was not
    /// labelled, so the output still reads as a condition when the column is not known.
    /// </remarks>
    public static ImmutableArray<PredicateToken> Write(SeekBounds bounds)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteBounds(tokens, bounds);

        return tokens.ToImmutable();
    }

    public static ImmutableArray<PredicateToken> Write(AccessStep.Probe probe)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteComparedKeys(tokens, probe.Key, probe.Target, probe.Width, probe.Comparison);

        return tokens.ToImmutable();
    }

    public static ImmutableArray<PredicateToken> Write(AccessStep.RangeEnd rangeEnd)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteComparedKeys(tokens, rangeEnd.Key, rangeEnd.Boundary, rangeEnd.Width, rangeEnd.Comparison);

        return tokens.ToImmutable();
    }

    private static void WriteComparedKeys(ImmutableArray<PredicateToken>.Builder tokens,
                                          in AccessKey key,
                                          in AccessKey target,
                                          int width,
                                          int comparison)
    {
        var length = Math.Min(width, Math.Min(key.Count, target.Count));

        if (length == 0)
        {
            return;
        }

        WriteKeyValues(tokens, key, length);

        Space(tokens);

        var description = comparison switch
        {
            < 0 => "<",
            0 => "=",
            > 0 => ">"
        };

        tokens.Add(new PredicateToken(PredicateTokenType.Operator, description));

        Space(tokens);

        WriteKeyValues(tokens, target, length);
    }

    public static ImmutableArray<PredicateToken> Write(AccessStep.ProbeResult probeResult)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteRuleCondition(tokens, probeResult.Rule, probeResult.Target, probeResult.Width);

        return tokens.ToImmutable();
    }

    public static ImmutableArray<PredicateToken> Write(AccessStep.ProbeStart probeStart)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteRuleCondition(tokens, probeStart.Rule, probeStart.Target, probeStart.Width);

        return tokens.ToImmutable();
    }

    private static void WriteRuleCondition(ImmutableArray<PredicateToken>.Builder tokens,
                                           SeekRule? rule,
                                           in AccessKey target,
                                           int width)
    {
        var length = Math.Min(width, target.Count);

        if (rule is null || length == 0)
        {
            return;
        }

        var symbol = rule switch
        {
            SeekRule.LowestGreaterOrEqual => ">=",
            SeekRule.LowestGreater => ">",
            SeekRule.HighestLessOrEqual => "<=",
            _ => "<"
        };

        tokens.Add(new PredicateToken(PredicateTokenType.Operator, symbol));

        Space(tokens);

        WriteKeyValues(tokens, target, length);
    }

    /// <summary>
    /// Flattens tokens to plain text
    /// </summary>
    public static string ToText(ImmutableArray<PredicateToken> tokens)
    {
        if (tokens.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        return string.Concat(tokens.Select(t => t.Text));
    }

    private static void WriteBounds(ImmutableArray<PredicateToken>.Builder tokens, SeekBounds bounds)
    {
        if (bounds is { HasStart: false, HasEnd: false })
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "ALL"));

            return;
        }

        // An inclusive range over the same value on both sides is an equality seek and reads better written that way
        if (bounds is { HasStart: true, HasEnd: true, IsStartInclusive: true, IsEndInclusive: true } &&
            bounds.StartValue.Equals(bounds.EndValue))
        {
            WriteKeyComparison(tokens, bounds.StartValue, "=");

            return;
        }

        var written = false;

        if (bounds.HasStart)
        {
            WriteKeyComparison(tokens, bounds.StartValue, bounds.IsStartInclusive ? ">=" : ">");

            written = true;
        }

        if (bounds.HasEnd)
        {
            if (written)
            {
                Space(tokens);

                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "AND"));

                Space(tokens);
            }

            WriteKeyComparison(tokens, bounds.EndValue, bounds.IsEndInclusive ? "<=" : "<");
        }
    }

    /// <summary>
    /// Writes a comparison between the key columns and a boundary value
    /// </summary>
    /// <remarks>
    /// A multi-column boundary is written as a row comparison so the leading column ordering that makes the seek possible stays visible.
    /// </remarks>
    private static void WriteKeyComparison(ImmutableArray<PredicateToken>.Builder tokens,
                                           AccessKey key,
                                           string comparison)
    {
        var isComposite = key.Count > 1;

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
        }

        for (var index = 0; index < key.Count; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ","));
                Space(tokens);
            }

            tokens.Add(new PredicateToken(PredicateTokenType.Column, KeyColumnName(key[index], index)));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }

        Space(tokens);
        tokens.Add(new PredicateToken(PredicateTokenType.Operator, comparison));
        Space(tokens);

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
        }

        for (var index = 0; index < key.Count; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ","));
                Space(tokens);
            }

            tokens.Add(AccessValueFormatter.Format(key[index]));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }
    }

    private static string KeyColumnName(AccessValue value, int index)
    {
        return value.ColumnName ?? $"Key{index + 1}";
    }

    internal static void WriteKeyValues(ImmutableArray<PredicateToken>.Builder tokens, AccessKey key, int length)
    {
        var isComposite = length > 1;

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
        }

        for (var index = 0; index < length; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ","));
                Space(tokens);
            }

            tokens.Add(AccessValueFormatter.Format(key[index]));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }
    }

    /// <summary>
    /// Writes a predicate, bracketing it when it sits inside a predicate that binds more tightly
    /// </summary>
    private static void WritePredicate(ImmutableArray<PredicateToken>.Builder tokens,
                                       AccessPredicate predicate,
                                       bool bracket)
    {
        switch (predicate)
        {
            case AccessPredicate.True:
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "TRUE"));
                break;

            case AccessPredicate.Comparison comparison:
                WriteComparison(tokens, comparison);
                break;

            case AccessPredicate.And and:
                WriteJunction(tokens, and.Predicates, "AND", bracket);
                break;

            case AccessPredicate.Or or:
                WriteJunction(tokens, or.Predicates, "OR", bracket);
                break;

            case AccessPredicate.Not not:
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "NOT"));
                Space(tokens);
                WritePredicate(tokens, not.Predicate, true);
                break;

            case AccessPredicate.IsNull isNull:
                WriteExpression(tokens, isNull.Expression);
                Space(tokens);
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "IS NULL"));
                break;

            case AccessPredicate.In inList:
                WriteIn(tokens, inList);
                break;

            case AccessPredicate.Like like:
                WriteExpression(tokens, like.Expression);

                Space(tokens);
                
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "LIKE"));
                
                Space(tokens);
                
                tokens.Add(new PredicateToken(PredicateTokenType.Literal, $"'{like.Pattern.Replace("'", "''")}'"));
                
                break;

            default:
                tokens.Add(new PredicateToken(PredicateTokenType.Unknown,
                                              "<unsupported>",
                                              predicate.GetType().Name));
                break;
        }
    }

    private static void WriteJunction(ImmutableArray<PredicateToken>.Builder tokens,
                                      ImmutableArray<AccessPredicate> predicates,
                                      string keyword,
                                      bool bracket)
    {
        if (predicates.IsDefaultOrEmpty)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "TRUE"));

            return;
        }

        if (predicates.Length == 1)
        {
            WritePredicate(tokens, predicates[0], bracket);

            return;
        }

        if (bracket)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
        }

        for (var index = 0; index < predicates.Length; index++)
        {
            if (index > 0)
            {
                Space(tokens);

                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, keyword));
                
                Space(tokens);
            }

            WritePredicate(tokens, predicates[index], true);
        }

        if (bracket)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }
    }

    private static void WriteComparison(ImmutableArray<PredicateToken>.Builder tokens,
                                        AccessPredicate.Comparison comparison)
    {
        WriteExpression(tokens, comparison.Left);

        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenType.Operator, Symbol(comparison.Operator)));
        
        Space(tokens);
        
        WriteExpression(tokens, comparison.Right);
    }

    private static void WriteIn(ImmutableArray<PredicateToken>.Builder tokens, AccessPredicate.In inList)
    {
        WriteExpression(tokens, inList.Expression);

        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "IN"));
        
        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));

        if (!inList.Values.IsDefaultOrEmpty)
        {
            for (var index = 0; index < inList.Values.Length; index++)
            {
                if (index > 0)
                {
                    tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ","));
                    Space(tokens);
                }

                WriteExpression(tokens, inList.Values[index]);
            }
        }

        tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
    }

    private static void WriteExpression(ImmutableArray<PredicateToken>.Builder tokens, AccessExpression expression)
    {
        switch (expression)
        {
            case AccessExpression.Column column:
                tokens.Add(new PredicateToken(PredicateTokenType.Column,
                                              column.Name,
                                              $"Ordinal {column.Ordinal}"));
                break;

            case AccessExpression.Constant constant:
                tokens.Add(AccessValueFormatter.Format(constant.Value));
                break;

            default:
                tokens.Add(new PredicateToken(PredicateTokenType.Unknown,
                                              "<unsupported>",
                                              expression.GetType().Name));
                break;
        }
    }

    private static string Symbol(ComparisonOperator comparison)
    {
        return comparison switch
        {
            ComparisonOperator.Equal => "=",
            ComparisonOperator.NotEqual => "<>",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            _ => "?"
        };
    }

    private static void Space(ImmutableArray<PredicateToken>.Builder tokens)
    {
        tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
    }
}
