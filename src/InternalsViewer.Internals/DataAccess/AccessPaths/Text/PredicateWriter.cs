using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;

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
    /// Bounds are written against the key columns they apply to when those columns are known, falling back to positional names so the
    /// output still reads as a condition when the index definition is not to hand.
    /// </remarks>
    public static ImmutableArray<PredicateToken> Write(SeekBounds bounds,
                                                       ImmutableArray<string> keyColumns = default)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        WriteBounds(tokens, bounds, keyColumns);

        return tokens.ToImmutable();
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

    private static void WriteBounds(ImmutableArray<PredicateToken>.Builder tokens,
                                    SeekBounds bounds,
                                    ImmutableArray<string> keyColumns)
    {
        if (bounds is { HasStart: false, HasEnd: false })
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "ALL"));

            return;
        }

        // An inclusive range over the same value on both sides is an equality seek and reads better written that way
        if (bounds is { HasStart: true, HasEnd: true, IsStartInclusive: true, IsEndInclusive: true } &&
            bounds.StartValue.Equals(bounds.EndValue))
        {
            WriteKeyComparison(tokens, bounds.StartValue, keyColumns, "=");

            return;
        }

        var written = false;

        if (bounds.HasStart)
        {
            WriteKeyComparison(tokens, bounds.StartValue, keyColumns, bounds.IsStartInclusive ? ">=" : ">");

            written = true;
        }

        if (bounds.HasEnd)
        {
            if (written)
            {
                Space(tokens);

                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "AND"));
                
                Space(tokens);
            }

            WriteKeyComparison(tokens, bounds.EndValue, keyColumns, bounds.IsEndInclusive ? "<=" : "<");
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
                                           ImmutableArray<string> keyColumns,
                                           string comparison)
    {
        var isComposite = key.Count > 1;

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, "("));
        }

        for (var index = 0; index < key.Count; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ","));
                Space(tokens);
            }

            tokens.Add(new PredicateToken(PredicateTokenKind.Column, KeyColumnName(keyColumns, index)));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ")"));
        }

        Space(tokens);
        tokens.Add(new PredicateToken(PredicateTokenKind.Operator, comparison));
        Space(tokens);

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, "("));
        }

        for (var index = 0; index < key.Count; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ","));
                Space(tokens);
            }

            tokens.Add(AccessValueFormatter.Format(key[index]));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ")"));
        }
    }

    private static string KeyColumnName(ImmutableArray<string> keyColumns, int index)
    {
        if (!keyColumns.IsDefaultOrEmpty && index < keyColumns.Length)
        {
            return keyColumns[index];
        }

        return $"Key{index + 1}";
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
                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "TRUE"));
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
                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "NOT"));
                Space(tokens);
                WritePredicate(tokens, not.Predicate, true);
                break;

            case AccessPredicate.IsNull isNull:
                WriteExpression(tokens, isNull.Expression);
                Space(tokens);
                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "IS NULL"));
                break;

            case AccessPredicate.In inList:
                WriteIn(tokens, inList);
                break;

            case AccessPredicate.Like like:
                WriteExpression(tokens, like.Expression);

                Space(tokens);
                
                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "LIKE"));
                
                Space(tokens);
                
                tokens.Add(new PredicateToken(PredicateTokenKind.Literal, $"'{like.Pattern.Replace("'", "''")}'"));
                
                break;

            default:
                tokens.Add(new PredicateToken(PredicateTokenKind.Unknown,
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
            tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "TRUE"));

            return;
        }

        if (predicates.Length == 1)
        {
            WritePredicate(tokens, predicates[0], bracket);

            return;
        }

        if (bracket)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, "("));
        }

        for (var index = 0; index < predicates.Length; index++)
        {
            if (index > 0)
            {
                Space(tokens);

                tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, keyword));
                
                Space(tokens);
            }

            WritePredicate(tokens, predicates[index], true);
        }

        if (bracket)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ")"));
        }
    }

    private static void WriteComparison(ImmutableArray<PredicateToken>.Builder tokens,
                                        AccessPredicate.Comparison comparison)
    {
        WriteExpression(tokens, comparison.Left);

        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenKind.Operator, Symbol(comparison.Operator)));
        
        Space(tokens);
        
        WriteExpression(tokens, comparison.Right);
    }

    private static void WriteIn(ImmutableArray<PredicateToken>.Builder tokens, AccessPredicate.In inList)
    {
        WriteExpression(tokens, inList.Expression);

        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenKind.Keyword, "IN"));
        
        Space(tokens);
        
        tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, "("));

        if (!inList.Values.IsDefaultOrEmpty)
        {
            for (var index = 0; index < inList.Values.Length; index++)
            {
                if (index > 0)
                {
                    tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ","));
                    Space(tokens);
                }

                WriteExpression(tokens, inList.Values[index]);
            }
        }

        tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ")"));
    }

    private static void WriteExpression(ImmutableArray<PredicateToken>.Builder tokens, AccessExpression expression)
    {
        switch (expression)
        {
            case AccessExpression.Column column:
                tokens.Add(new PredicateToken(PredicateTokenKind.Column,
                                              column.Name,
                                              $"Ordinal {column.Ordinal}"));
                break;

            case AccessExpression.Constant constant:
                tokens.Add(AccessValueFormatter.Format(constant.Value));
                break;

            default:
                tokens.Add(new PredicateToken(PredicateTokenKind.Unknown,
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
        tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));
    }
}
