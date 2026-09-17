using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Execution.AccessPaths.Values;

/// <summary>
/// Compares <see cref="AccessValue"/> instances using index key ordering
/// </summary>
/// <remarks>
/// NULL sorts lower than any other value, matching SQL Server index ordering. Variable length values are compared ordinally, which matches
/// binary collations but not linguistic ones.
/// </remarks>
internal static class AccessValueComparer
{
    private static readonly CompareInfo TextComparer = CultureInfo.InvariantCulture.CompareInfo;

    public static int Compare(in AccessValue left, in AccessValue right)
    {
        if (left.IsNull || right.IsNull)
        {
            return (left.IsNull, right.IsNull) switch
            {
                (true, true) 
                    => 0,
                (true, false) 
                    => -1,
                _ => 1
            };
        }

        if (left.Type == right.Type)
        {
            return CompareSameType(left, right);
        }

        return CompareMixedType(left, right);
    }

    internal static bool IsCharacterType(SqlDbType dataType)
    {
        return dataType is SqlDbType.Char or SqlDbType.VarChar or SqlDbType.Text
                        or SqlDbType.NChar or SqlDbType.NVarChar or SqlDbType.NText;
    }

    internal static bool IsWideCharacterType(SqlDbType dataType)
    {
        return dataType is SqlDbType.NChar or SqlDbType.NVarChar or SqlDbType.NText;
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
                if (IsCharacterType(left.DataType) && IsCharacterType(right.DataType))
                {
                    return CompareText(left, right);
                }

                return left.Data.Span.SequenceCompareTo(right.Data.Span);
        }
    }

    private static int CompareText(in AccessValue left, in AccessValue right)
    {
        var leftChars = IsWideCharacterType(left.DataType)
            ? MemoryMarshal.Cast<byte, char>(left.Data.Span)
            : Encoding.Latin1.GetString(left.Data.Span).AsSpan();

        var rightChars = IsWideCharacterType(right.DataType)
            ? MemoryMarshal.Cast<byte, char>(right.Data.Span)
            : Encoding.Latin1.GetString(right.Data.Span).AsSpan();

        return TextComparer.Compare(leftChars.TrimEnd(' '), rightChars.TrimEnd(' '), CompareOptions.IgnoreCase);
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

        if (TryCompareTemporalToText(left, right, out var temporal))
        {
            return temporal;
        }

        throw new InvalidOperationException(
            $"Cannot compare a value of type {left.Type} with a value of type {right.Type}.");
    }

    /// <summary>
    /// Compares a temporal value held as a tick count against a character literal by parsing the literal to the same type
    /// </summary>
    /// <remarks>
    /// A date, time or datetimeoffset column normalizes to a tick count, but a literal written in the query arrives as a string, so the
    /// string is parsed with the invariant ISO forms showplan writes and the two tick counts are compared.
    /// </remarks>
    private static bool TryCompareTemporalToText(in AccessValue left, in AccessValue right, out int result)
    {
        if (IsTemporal(left) && IsCharacter(right) && TryParseTemporalTicks(left.DataType, GetText(right), out var rightTicks))
        {
            result = left.Numeric.CompareTo(rightTicks);

            return true;
        }

        if (IsTemporal(right) && IsCharacter(left) && TryParseTemporalTicks(right.DataType, GetText(left), out var leftTicks))
        {
            result = leftTicks.CompareTo(right.Numeric);

            return true;
        }

        result = 0;

        return false;
    }

    private static bool IsTemporal(in AccessValue value)
        => value.Type == AccessValueType.Integer && value.DataType is SqlDbType.Date or SqlDbType.DateTime or SqlDbType.SmallDateTime
                                                                    or SqlDbType.DateTime2 or SqlDbType.Time or SqlDbType.DateTimeOffset;

    private static bool IsCharacter(in AccessValue value) => value.Type == AccessValueType.Bytes && IsCharacterType(value.DataType);

    private static string GetText(in AccessValue value)
        => IsWideCharacterType(value.DataType)
           ? new string(MemoryMarshal.Cast<byte, char>(value.Data.Span))
           : Encoding.Latin1.GetString(value.Data.Span);

    private static bool TryParseTemporalTicks(SqlDbType dataType, string text, out long ticks)
    {
        var trimmed = text.Trim();

        switch (dataType)
        {
            case SqlDbType.Time:
                if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var span))
                {
                    ticks = span.Ticks;

                    return true;
                }

                break;

            case SqlDbType.DateTimeOffset:
                if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset))
                {
                    ticks = offset.UtcTicks;

                    return true;
                }

                break;

            default:
                if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
                {
                    ticks = moment.Ticks;

                    return true;
                }

                break;
        }

        ticks = 0;

        return false;
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
