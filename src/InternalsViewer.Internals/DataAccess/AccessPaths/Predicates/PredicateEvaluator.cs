using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;

/// <summary>
/// Evaluates predicates against a row using SQL three stage logic (true/false/unknown)
/// </summary>
/// <remarks>
/// A null result represents unknown. Comparisons involving a NULL operand are unknown, which is why a residual predicate can reject a row
/// without the row being false.
/// </remarks>
public static class PredicateEvaluator
{
    public static bool? Evaluate(AccessPredicate predicate, IRowValueSource row)
    {
        return predicate switch
        {
            AccessPredicate.True 
                => true,
            AccessPredicate.Comparison comparison 
                => EvaluateComparison(comparison, row),
            AccessPredicate.And and 
                => EvaluateAnd(and, row),
            AccessPredicate.Or or 
                => EvaluateOr(or, row),
            AccessPredicate.Not not 
                => Negate(Evaluate(not.Predicate, row)),
            AccessPredicate.IsNull isNull 
                => Resolve(isNull.Expression, row).IsNull,
            AccessPredicate.In inPredicate 
                => EvaluateIn(inPredicate, row),
            AccessPredicate.Like like 
                => EvaluateLike(like, row),
            _ => 
                throw new NotSupportedException($"Predicate {predicate.GetType().Name} is not supported.")
        };
    }

    /// <summary>
    /// Resolves a scalar expression to a value
    /// </summary>
    public static AccessValue Resolve(AccessExpression expression, IRowValueSource row)
    {
        return expression switch
        {
            AccessExpression.Constant constant
                => constant.Value,
            AccessExpression.Column column
                => row.GetValue(column.Ordinal, column.Name),
            AccessExpression.Arithmetic arithmetic
                => ResolveArithmetic(arithmetic, row),
            _ => throw new NotSupportedException(
                $"Expression {expression.GetType().Name} is not supported.")
        };
    }

    private static AccessValue ResolveArithmetic(AccessExpression.Arithmetic arithmetic, IRowValueSource row)
    {
        var left = Resolve(arithmetic.Left, row);
        var right = Resolve(arithmetic.Right, row);

        if (left.IsNull || right.IsNull)
        {
            return AccessValue.Null;
        }

        if (left.Type == AccessValueType.Real || right.Type == AccessValueType.Real)
        {
            var result = ApplyReal(arithmetic.Operator, ToReal(left), ToReal(right));

            return result is null ? AccessValue.Null : AccessValue.FromReal(SqlDbType.Float, result.Value);
        }

        if (left.Type == AccessValueType.Decimal || right.Type == AccessValueType.Decimal)
        {
            var result = ApplyDecimal(arithmetic.Operator, ToDecimal(left), ToDecimal(right));

            return result is null ? AccessValue.Null : AccessValue.FromDecimal(SqlDbType.Decimal, result.Value);
        }

        if (left.Type == AccessValueType.Integer && right.Type == AccessValueType.Integer)
        {
            var result = ApplyInteger(arithmetic.Operator, left.Numeric, right.Numeric);

            return result is null ? AccessValue.Null : AccessValue.FromInteger(SqlDbType.BigInt, result.Value);
        }

        return AccessValue.Null;
    }

    private static long? ApplyInteger(ArithmeticOperator op, long left, long right)
    {
        return op switch
        {
            ArithmeticOperator.Add => left + right,
            ArithmeticOperator.Subtract => left - right,
            ArithmeticOperator.Multiply => left * right,
            ArithmeticOperator.Divide => right == 0 ? null : left / right,
            ArithmeticOperator.Modulo => right == 0 ? null : left % right,
            _ => null
        };
    }

    private static decimal? ApplyDecimal(ArithmeticOperator op, decimal left, decimal right)
    {
        return op switch
        {
            ArithmeticOperator.Add => left + right,
            ArithmeticOperator.Subtract => left - right,
            ArithmeticOperator.Multiply => left * right,
            ArithmeticOperator.Divide => right == 0 ? null : left / right,
            ArithmeticOperator.Modulo => right == 0 ? null : left % right,
            _ => null
        };
    }

    private static double? ApplyReal(ArithmeticOperator op, double left, double right)
    {
        return op switch
        {
            ArithmeticOperator.Add => left + right,
            ArithmeticOperator.Subtract => left - right,
            ArithmeticOperator.Multiply => left * right,
            ArithmeticOperator.Divide => right == 0 ? null : left / right,
            ArithmeticOperator.Modulo => right == 0 ? null : left % right,
            _ => null
        };
    }

    private static double ToReal(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Integer => value.Numeric,
            AccessValueType.Real => value.Real,
            _ => (double)value.ToDecimal()
        };
    }

    private static decimal ToDecimal(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Integer => value.Numeric,
            AccessValueType.Real => (decimal)value.Real,
            _ => value.ToDecimal()
        };
    }

    private static bool? EvaluateComparison(AccessPredicate.Comparison comparison, IRowValueSource row)
    {
        var left = Resolve(comparison.Left, row);
        var right = Resolve(comparison.Right, row);

        if (left.IsNull || right.IsNull)
        {
            return null;
        }

        var result = AccessValueComparer.Compare(left, right);

        return comparison.Operator switch
        {
            ComparisonOperator.Equal 
                => result == 0,
            ComparisonOperator.NotEqual 
                => result != 0,
            ComparisonOperator.LessThan 
                => result < 0,
            ComparisonOperator.LessThanOrEqual 
                => result <= 0,
            ComparisonOperator.GreaterThan 
                => result > 0,
            ComparisonOperator.GreaterThanOrEqual 
                => result >= 0,
            _ => throw new NotSupportedException($"Operator {comparison.Operator} is not supported.")
        };
    }

    private static bool? EvaluateAnd(AccessPredicate.And and, IRowValueSource row)
    {
        var unknown = false;

        foreach (var predicate in and.Predicates)
        {
            var result = Evaluate(predicate, row);

            if (result == false)
            {
                return false;
            }

            if (result is null)
            {
                unknown = true;
            }
        }

        return unknown ? null : true;
    }

    private static bool? EvaluateOr(AccessPredicate.Or or, IRowValueSource row)
    {
        var unknown = false;

        foreach (var predicate in or.Predicates)
        {
            var result = Evaluate(predicate, row);

            if (result == true)
            {
                return true;
            }

            if (result is null)
            {
                unknown = true;
            }
        }

        return unknown ? null : false;
    }

    private static bool? EvaluateIn(AccessPredicate.In inPredicate, IRowValueSource row)
    {
        var value = Resolve(inPredicate.Expression, row);

        if (value.IsNull)
        {
            return null;
        }

        var unknown = false;

        foreach (var candidate in inPredicate.Values)
        {
            var other = Resolve(candidate, row);

            if (other.IsNull)
            {
                unknown = true;

                continue;
            }

            if (AccessValueComparer.Compare(value, other) == 0)
            {
                return true;
            }
        }

        return unknown ? null : false;
    }

    private static bool? EvaluateLike(AccessPredicate.Like like, IRowValueSource row)
    {
        var value = Resolve(like.Expression, row);

        if (value.IsNull)
        {
            return null;
        }

        var text = ToText(value);

        if (text is null)
        {
            return null;
        }

        var pattern = "^" + Regex.Escape(like.Pattern).Replace("%", ".*").Replace("_", ".") + "$";

        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool? Negate(bool? value)
    {
        return value switch
        {
            true => false,
            false => true,
            _ => null
        };
    }

    private static string? ToText(in AccessValue value)
    {
        if (value.Type != AccessValueType.Bytes)
        {
            return null;
        }

        return value.DataType switch
        {
            SqlDbType.Char
                or SqlDbType.VarChar
                or SqlDbType.Text
                or SqlDbType.NChar
                or SqlDbType.NVarChar
                or SqlDbType.NText => Encoding.Unicode.GetString(value.Data.Span),
            _ => null
        };
    }
}
