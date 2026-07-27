using System.Data;
using System.Globalization;
using System.Text;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Parses the literal text showplan places in a ConstValue attribute
/// </summary>
/// <remarks>
/// Showplan renders a constant the way it would appear in Transact-SQL rather than as a typed value, so the type has to be recovered from
/// the way the literal is written. Strings arrive quoted with doubled inner quotes, unicode strings carry an N prefix, and binary arrives
/// with a 0x prefix.
/// </remarks>
public static class ConstValueParser
{
    /// <summary>
    /// Parses a constant literal, returning a null value when the literal cannot be interpreted
    /// </summary>
    public static AccessValue Parse(string? literal)
    {
        if (string.IsNullOrWhiteSpace(literal))
        {
            return AccessValue.Null;
        }

        var text = literal.Trim();

        // Showplan brackets a constant it produced itself, such as (42) for a compiled parameter value
        while (text.Length > 2 && text[0] == '(' && text[^1] == ')')
        {
            text = text[1..^1].Trim();
        }

        if (string.Equals(text, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return AccessValue.Null;
        }

        if (TryParseString(text, out var value))
        {
            return value;
        }

        if (TryParseBinary(text, out value))
        {
            return value;
        }

        return ParseNumeric(text);
    }

    private static bool TryParseString(string text, out AccessValue value)
    {
        value = AccessValue.Null;

        var isUnicode = text.Length > 1 && text[0] is 'N' or 'n' && text[1] == '\'';

        var start = isUnicode ? 1 : 0;

        if (text.Length - start < 2 || text[start] != '\'' || !text.EndsWith('\''))
        {
            return false;
        }

        var inner = text.Substring(start + 1, text.Length - start - 2).Replace("''", "'");

        var dataType = isUnicode ? SqlDbType.NVarChar : SqlDbType.VarChar;

        var bytes = isUnicode ? Encoding.Unicode.GetBytes(inner) : Encoding.ASCII.GetBytes(inner);

        value = AccessValue.FromBytes(dataType, bytes);

        return true;
    }

    private static bool TryParseBinary(string text, out AccessValue value)
    {
        value = AccessValue.Null;

        if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = text[2..];

        if (digits.Length == 0 || digits.Length % 2 != 0)
        {
            return false;
        }

        var bytes = new byte[digits.Length / 2];

        for (var index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(digits.AsSpan(index * 2, 2),
                               NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture,
                               out var parsed))
            {
                return false;
            }

            bytes[index] = parsed;
        }

        value = AccessValue.FromBytes(SqlDbType.VarBinary, bytes);

        return true;
    }

    /// <summary>
    /// Parses a numeric literal, preferring exact types so comparisons stay exact
    /// </summary>
    /// <remarks>
    /// A literal without a decimal point is an integer, one with a decimal point but no exponent is treated as decimal rather than float,
    /// matching how the engine types a numeric literal.
    /// </remarks>
    private static AccessValue ParseNumeric(string text)
    {
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return AccessValue.FromInteger(SqlDbType.BigInt, integer);
        }

        var hasExponent = text.Contains('e', StringComparison.OrdinalIgnoreCase);

        if (!hasExponent &&
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var exact))
        {
            return AccessValue.FromDecimal(SqlDbType.Decimal, exact);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return AccessValue.FromReal(SqlDbType.Float, real);
        }

        return AccessValue.Null;
    }
}
