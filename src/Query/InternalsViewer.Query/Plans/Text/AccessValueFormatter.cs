using System.Data;
using System.Globalization;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.AccessPaths.Text;

/// <summary>
/// Renders a value the way it would be written in Transact-SQL
/// </summary>
/// <remarks>
/// A value keeps its payload in the form the storage engine uses, so the data type decides how the bytes should be read back. A character
/// type is decoded to text and quoted, anything else falls back to a binary literal.
/// </remarks>
internal static class AccessValueFormatter
{
    /// <summary>
    /// Formats a value as a literal, returning the token role the literal takes
    /// </summary>
    public static PredicateToken Format(AccessValue value)
    {
        if (value.IsNull)
        {
            return new PredicateToken(PredicateTokenType.Null, "NULL");
        }

        return value.Type switch
        {
            AccessValueType.Integer 
                => new PredicateToken(PredicateTokenType.Number, FormatInteger(value)),
            AccessValueType.Real 
                => new PredicateToken(PredicateTokenType.Number,
                                      value.Real.ToString("R", CultureInfo.InvariantCulture)),
            AccessValueType.Decimal 
                => new PredicateToken(PredicateTokenType.Number,
                                      value.ToDecimal().ToString(CultureInfo.InvariantCulture)),
            _ => 
                new PredicateToken(PredicateTokenType.Literal, FormatBytes(value))
        };
    }

    /// <summary>
    /// Formats a value as plain text without any token role
    /// </summary>
    public static string ToText(AccessValue value)
    {
        return Format(value).Text;
    }

    /// <summary>
    /// Formats an integral payload, recovering the types that borrow it
    /// </summary>
    /// <remarks>
    /// A bit is stored as an integer but reads better as 0 or 1, and a date or time is stored as its engine representation, which is not
    /// meaningful on its own, so it is left as the raw number rather than guessed at.
    /// </remarks>
    public static bool TryFormatTemporal(AccessValue value, out string text)
    {
        if (value.Type != AccessValueType.Integer)
        {
            text = string.Empty;

            return false;
        }

        text = value.DataType switch
        {
            SqlDbType.Date
                => new DateTime(value.Numeric).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SqlDbType.SmallDateTime
                => new DateTime(value.Numeric).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            SqlDbType.DateTime
                => new DateTime(value.Numeric).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            SqlDbType.DateTime2
                => new DateTime(value.Numeric).ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            SqlDbType.Time
                => new TimeSpan(value.Numeric).ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture),
            SqlDbType.DateTimeOffset
                => new DateTimeOffset(value.Numeric, TimeSpan.Zero).ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz",
                                                                             CultureInfo.InvariantCulture),
            _ => string.Empty
        };

        return text.Length > 0;
    }

    private static string FormatInteger(AccessValue value)
    {
        if (value.DataType == SqlDbType.Bit)
        {
            return value.Numeric == 0 ? "0" : "1";
        }

        if (TryFormatTemporal(value, out var temporal))
        {
            return temporal;
        }

        return value.Numeric.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(AccessValue value)
    {
        var span = value.Data.Span;

        switch (value.DataType)
        {
            case SqlDbType.Char:
            case SqlDbType.VarChar:
            case SqlDbType.Text:
                return Quote(Encoding.Latin1.GetString(span), false);

            case SqlDbType.NChar:
            case SqlDbType.NVarChar:
            case SqlDbType.NText:
                return Quote(Encoding.Unicode.GetString(span), true);

            case SqlDbType.UniqueIdentifier when span.Length == 16:
                return Quote(new Guid(span).ToString(), false);

            default:
                return span.IsEmpty ? "0x" : $"0x{Convert.ToHexString(span)}";
        }
    }

    private static string Quote(string text, bool isUnicode)
    {
        var escaped = text.Replace("'", "''");

        return isUnicode ? $"N'{escaped}'" : $"'{escaped}'";
    }
}
