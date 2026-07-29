namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// Compares <see cref="AccessValue"/> instances using index key ordering
/// </summary>
/// <remarks>
/// NULL sorts lower than any other value, matching SQL Server index ordering. Variable length values are compared ordinally, which matches
/// binary collations but not linguistic ones.
/// </remarks>
public static class AccessValueComparer
{
    public static int Compare(in AccessValue left, in AccessValue right)
    {
        if (left.IsNull || right.IsNull)
        {
            return (left.IsNull, right.IsNull) switch
            {
                (true, true) => 0,
                (true, false) => -1,
                _ => 1
            };
        }

        if (left.Type == right.Type)
        {
            return CompareSameType(left, right);
        }

        return CompareMixedType(left, right);
    }

    private static int CompareSameType(in AccessValue left, in AccessValue right)
    {
        switch (left.Type)
        {
            case AccessValueType.Integer:
                return left.Numeric.CompareTo(right.Numeric);

            case AccessValueType.Real:
                return left.Real.CompareTo(right.Real);

            case AccessValueType.Decimal:
                return left.ToDecimal().CompareTo(right.ToDecimal());

            default:
                return left.Data.Span.SequenceCompareTo(right.Data.Span);
        }
    }

    private static int CompareMixedType(in AccessValue left, in AccessValue right)
    {
        if (IsNumeric(left.Type) && IsNumeric(right.Type))
        {
            if (left.Type == AccessValueType.Decimal || right.Type == AccessValueType.Decimal)
            {
                return ToDecimalValue(left).CompareTo(ToDecimalValue(right));
            }

            return ToRealValue(left).CompareTo(ToRealValue(right));
        }

        throw new InvalidOperationException(
            $"Cannot compare a value of type {left.Type} with a value of type {right.Type}.");
    }

    private static bool IsNumeric(AccessValueType type)
    {
        return type is AccessValueType.Integer or AccessValueType.Real or AccessValueType.Decimal;
    }

    private static double ToRealValue(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Integer 
                => value.Numeric,
            AccessValueType.Real 
                => value.Real,
            _ => 
                (double)value.ToDecimal()
        };
    }

    private static decimal ToDecimalValue(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Integer 
                => value.Numeric,
            AccessValueType.Real 
                => (decimal)value.Real,
            _ => value.ToDecimal()
        };
    }
}
