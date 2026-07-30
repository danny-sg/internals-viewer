using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.AccessPaths.Predicates;

public static class IntrinsicFunctions
{
    private static readonly Dictionary<string, (int MinArguments, int MaxArguments)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABS"] = (1, 1),
        ["SIGN"] = (1, 1),
        ["FLOOR"] = (1, 1),
        ["CEILING"] = (1, 1),
        ["ROUND"] = (2, 2),
        ["POWER"] = (2, 2),
        ["LEN"] = (1, 1),
        ["UPPER"] = (1, 1),
        ["LOWER"] = (1, 1),
        ["LTRIM"] = (1, 1),
        ["RTRIM"] = (1, 1),
        ["LEFT"] = (2, 2),
        ["RIGHT"] = (2, 2),
        ["SUBSTRING"] = (3, 3),
        ["CHARINDEX"] = (2, 3),
        ["REPLACE"] = (3, 3),
        ["CONCAT"] = (2, 254),
        ["CONCAT_WS"] = (3, 254),
        ["ISNULL"] = (2, 2),
        ["GETDATE"] = (0, 0),
        ["GETUTCDATE"] = (0, 0),
        ["SYSDATETIME"] = (0, 0),
        ["SYSUTCDATETIME"] = (0, 0)
    };

    public static bool IsSupported(string name, int argumentCount)
    {
        return Supported.TryGetValue(name, out var arity)
               && argumentCount >= arity.MinArguments
               && argumentCount <= arity.MaxArguments;
    }

    public static AccessValue Apply(string name, IReadOnlyList<AccessValue> arguments, EvaluationContext context)
    {
        switch (name.ToUpperInvariant())
        {
            case "GETDATE":
                return AccessValue.FromInteger(SqlDbType.DateTime, context.QueryTime.Ticks);

            case "GETUTCDATE":
                return AccessValue.FromInteger(SqlDbType.DateTime, context.QueryTime.ToUniversalTime().Ticks);

            case "SYSDATETIME":
                return AccessValue.FromInteger(SqlDbType.DateTime2, context.QueryTime.Ticks);

            case "SYSUTCDATETIME":
                return AccessValue.FromInteger(SqlDbType.DateTime2, context.QueryTime.ToUniversalTime().Ticks);

            case "ISNULL":
                return arguments[0].IsNull ? arguments[1] : arguments[0];

            case "CONCAT":
                return ApplyConcat(arguments);

            case "CONCAT_WS":
                return ApplyConcatWs(arguments);
        }

        if (arguments.Any(a => a.IsNull))
        {
            return AccessValue.Null;
        }

        return name.ToUpperInvariant() switch
        {
            "ABS" 
                => ApplyNumericUnary(arguments[0], Math.Abs, Math.Abs, Math.Abs),
            "SIGN" 
                => ApplyNumericUnary(arguments[0], n => Math.Sign(n), d => Math.Sign(d), r => Math.Sign(r)),
            "FLOOR" 
                => ApplyNumericUnary(arguments[0], n => n, decimal.Floor, Math.Floor),
            "CEILING" 
                => ApplyNumericUnary(arguments[0], n => n, decimal.Ceiling, Math.Ceiling),
            "ROUND" 
                => ApplyRound(arguments[0], arguments[1]),
            "POWER" 
                => ApplyPower(arguments[0], arguments[1]),
            "LEN" 
                => ApplyText(arguments[0], t => AccessValue.FromInteger(SqlDbType.Int, t.TrimEnd(' ').Length)),
            "UPPER" 
                => ApplyText(arguments[0], t => Text(CultureInfo.InvariantCulture.TextInfo.ToUpper(t))),
            "LOWER" 
                => ApplyText(arguments[0], t => Text(CultureInfo.InvariantCulture.TextInfo.ToLower(t))),
            "LTRIM" 
                => ApplyText(arguments[0], t => Text(t.TrimStart(' '))),
            "RTRIM" 
                => ApplyText(arguments[0], t => Text(t.TrimEnd(' '))),
            "LEFT" 
                => ApplyLeft(arguments[0], arguments[1]),
            "RIGHT" 
                => ApplyRight(arguments[0], arguments[1]),
            "SUBSTRING" 
                => ApplySubstring(arguments[0], arguments[1], arguments[2]),
            "CHARINDEX" 
                => ApplyCharIndex(arguments),
            "REPLACE" 
                => ApplyReplace(arguments[0], arguments[1], arguments[2]),
            _ => AccessValue.Null
        };
    }

    internal static string? GetText(in AccessValue value)
    {
        if (value.Type != AccessValueType.Bytes || !AccessValueComparer.IsCharacterType(value.DataType))
        {
            return null;
        }

        var span = value.Data.Span;

        return AccessValueComparer.IsWideCharacterType(value.DataType)
            ? new string(MemoryMarshal.Cast<byte, char>(span))
            : Encoding.Latin1.GetString(span);
    }

    private static AccessValue ApplyNumericUnary(in AccessValue value,
                                                 Func<long, long> integer,
                                                 Func<decimal, decimal> exact,
                                                 Func<double, double> real)
    {
        return value.Type switch
        {
            AccessValueType.Integer 
                => AccessValue.FromInteger(value.DataType, integer(value.Numeric)),
            AccessValueType.Decimal 
                => AccessValue.FromDecimal(value.DataType, exact(value.ToDecimal())),
            AccessValueType.Real 
                => AccessValue.FromReal(value.DataType, real(value.Real)),
            _ => AccessValue.Null
        };
    }

    private static AccessValue ApplyRound(in AccessValue value, in AccessValue length)
    {
        if (GetInteger(length) is not { } digits)
        {
            return AccessValue.Null;
        }

        return value.Type switch
        {
            AccessValueType.Integer
                => digits >= 0
                    ? value
                    : AccessValue.FromInteger(value.DataType, (long)RoundDecimal(value.Numeric, digits)),
            AccessValueType.Decimal
                => AccessValue.FromDecimal(value.DataType, RoundDecimal(value.ToDecimal(), digits)),
            AccessValueType.Real
                => AccessValue.FromReal(value.DataType, RoundReal(value.Real, digits)),
            _ => AccessValue.Null
        };
    }

    private static decimal RoundDecimal(decimal value, long digits)
    {
        if (digits >= 0)
        {
            return Math.Round(value, (int)Math.Min(digits, 28), MidpointRounding.AwayFromZero);
        }

        if (digits < -28)
        {
            return 0;
        }

        var scale = 1m;

        for (var i = 0; i < -digits; i++)
        {
            scale *= 10;
        }

        return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
    }

    private static double RoundReal(double value, long digits)
    {
        if (digits >= 0)
        {
            return Math.Round(value, (int)Math.Min(digits, 15), MidpointRounding.AwayFromZero);
        }

        var scale = Math.Pow(10, -digits);

        return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
    }

    private static AccessValue ApplyPower(in AccessValue value, in AccessValue exponent)
    {
        var result = Math.Pow(ToReal(value), ToReal(exponent));

        if (value.Type == AccessValueType.Integer && exponent.Type == AccessValueType.Integer)
        {
            return AccessValue.FromInteger(SqlDbType.BigInt, (long)result);
        }

        return AccessValue.FromReal(SqlDbType.Float, result);
    }

    private static AccessValue ApplyText(in AccessValue value, Func<string, AccessValue> apply)
    {
        return GetText(value) is { } text ? apply(text) : AccessValue.Null;
    }

    private static AccessValue ApplyLeft(in AccessValue value, in AccessValue count)
    {
        var length = GetInteger(count);

        if (GetText(value) is not { } text || length is null or < 0)
        {
            return AccessValue.Null;
        }

        return Text(text[..(int)Math.Min(length.Value, text.Length)]);
    }

    private static AccessValue ApplyRight(in AccessValue value, in AccessValue count)
    {
        var length = GetInteger(count);

        if (GetText(value) is not { } text || length is null or < 0)
        {
            return AccessValue.Null;
        }

        return Text(text[^(int)Math.Min(length.Value, text.Length)..]);
    }

    private static AccessValue ApplySubstring(in AccessValue value, in AccessValue start, in AccessValue count)
    {
        var length = GetInteger(count);

        if (GetText(value) is not { } text || GetInteger(start) is not { } from || length is null or < 0)
        {
            return AccessValue.Null;
        }

        var effectiveLength = length.Value - Math.Max(1 - from, 0);

        var begin = Math.Max(from, 1) - 1;

        if (effectiveLength <= 0 || begin >= text.Length)
        {
            return Text(string.Empty);
        }

        return Text(text.Substring((int)begin, (int)Math.Min(effectiveLength, text.Length - begin)));
    }

    private static AccessValue ApplyCharIndex(IReadOnlyList<AccessValue> arguments)
    {
        if (GetText(arguments[0]) is not { } find || GetText(arguments[1]) is not { } source)
        {
            return AccessValue.Null;
        }

        var start = arguments.Count > 2 ? GetInteger(arguments[2]) : 1;

        if (start is null)
        {
            return AccessValue.Null;
        }

        var from = (int)Math.Max(start.Value, 1) - 1;

        if (find.Length == 0 || from >= source.Length)
        {
            return AccessValue.FromInteger(SqlDbType.Int, 0);
        }

        var index = CultureInfo.InvariantCulture.CompareInfo.IndexOf(source, find, from, CompareOptions.IgnoreCase);

        return AccessValue.FromInteger(SqlDbType.Int, index + 1);
    }

    private static AccessValue ApplyReplace(in AccessValue value, in AccessValue find, in AccessValue replacement)
    {
        if (GetText(value) is not { } text || GetText(find) is not { } pattern || GetText(replacement) is not { } replaceWith)
        {
            return AccessValue.Null;
        }

        if (pattern.Length == 0)
        {
            return Text(text);
        }

        return Text(text.Replace(pattern, replaceWith, StringComparison.InvariantCultureIgnoreCase));
    }

    private static AccessValue ApplyConcat(IReadOnlyList<AccessValue> arguments)
    {
        var builder = new StringBuilder();

        foreach (var argument in arguments)
        {
            if (argument.IsNull)
            {
                continue;
            }

            if (ValueText(argument) is not { } text)
            {
                return AccessValue.Null;
            }

            builder.Append(text);
        }

        return Text(builder.ToString());
    }

    private static AccessValue ApplyConcatWs(IReadOnlyList<AccessValue> arguments)
    {
        var separator = arguments[0].IsNull ? string.Empty : ValueText(arguments[0]);

        if (separator is null)
        {
            return AccessValue.Null;
        }

        var parts = new List<string>();

        for (var index = 1; index < arguments.Count; index++)
        {
            if (arguments[index].IsNull)
            {
                continue;
            }

            if (ValueText(arguments[index]) is not { } text)
            {
                return AccessValue.Null;
            }

            parts.Add(text);
        }

        return Text(string.Join(separator, parts));
    }

    private static string? ValueText(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Bytes
                => GetText(value),
            AccessValueType.Integer when value.DataType
                is SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt or SqlDbType.Bit
                => value.Numeric.ToString(CultureInfo.InvariantCulture),
            AccessValueType.Decimal
                => value.ToDecimal().ToString(CultureInfo.InvariantCulture),
            AccessValueType.Real
                => value.Real.ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static AccessValue Text(string value)
    {
        return AccessValueFactory.FromText(SqlDbType.NVarChar, value);
    }

    private static long? GetInteger(in AccessValue value)
    {
        return value.Type switch
        {
            AccessValueType.Integer => value.Numeric,
            AccessValueType.Decimal => (long)value.ToDecimal(),
            AccessValueType.Real => (long)value.Real,
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
}
