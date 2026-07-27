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

        if (left.Kind == right.Kind)
        {
            return CompareSameKind(left, right);
        }

        return CompareMixedKind(left, right);
    }

    private static int CompareSameKind(in AccessValue left, in AccessValue right)
    {
        switch (left.Kind)
        {
            case AccessValueKind.Integer:
                return left.Numeric.CompareTo(right.Numeric);

            case AccessValueKind.Real:
                return left.Real.CompareTo(right.Real);

            case AccessValueKind.Decimal:
                return left.ToDecimal().CompareTo(right.ToDecimal());

            default:
                return left.Data.Span.SequenceCompareTo(right.Data.Span);
        }
    }

    private static int CompareMixedKind(in AccessValue left, in AccessValue right)
    {
        if (IsNumeric(left.Kind) && IsNumeric(right.Kind))
        {
            if (left.Kind == AccessValueKind.Decimal || right.Kind == AccessValueKind.Decimal)
            {
                return ToDecimalValue(left).CompareTo(ToDecimalValue(right));
            }

            return ToRealValue(left).CompareTo(ToRealValue(right));
        }

        throw new InvalidOperationException(
            $"Cannot compare a value of kind {left.Kind} with a value of kind {right.Kind}.");
    }

    private static bool IsNumeric(AccessValueKind kind)
    {
        return kind is AccessValueKind.Integer or AccessValueKind.Real or AccessValueKind.Decimal;
    }

    private static double ToRealValue(in AccessValue value)
    {
        return value.Kind switch
        {
            AccessValueKind.Integer => value.Numeric,
            AccessValueKind.Real => value.Real,
            _ => (double)value.ToDecimal()
        };
    }

    private static decimal ToDecimalValue(in AccessValue value)
    {
        return value.Kind switch
        {
            AccessValueKind.Integer => value.Numeric,
            AccessValueKind.Real => (decimal)value.Real,
            _ => value.ToDecimal()
        };
    }
}
