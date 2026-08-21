using System.Collections.Generic;
using System.Data;

namespace InternalsViewer.UI.App.Helpers;

public static class SqlDataTypeFormat
{
    private static readonly HashSet<SqlDbType> WideTypes = [SqlDbType.NChar, SqlDbType.NVarChar, SqlDbType.NText];

    private static readonly HashSet<SqlDbType> LengthTypes =
    [
        SqlDbType.Char,
        SqlDbType.VarChar,
        SqlDbType.NChar,
        SqlDbType.NVarChar,
        SqlDbType.Binary,
        SqlDbType.VarBinary
    ];

    private static readonly HashSet<SqlDbType> ScaleTypes =
    [
        SqlDbType.DateTime2,
        SqlDbType.Time,
        SqlDbType.DateTimeOffset
    ];

    public static string GetName(SqlDbType type) => type switch
    {
        SqlDbType.Variant => "SQL_VARIANT",
        SqlDbType.UniqueIdentifier => "UNIQUEIDENTIFIER",
        SqlDbType.DateTimeOffset => "DATETIMEOFFSET",
        _ => type.ToString().ToUpperInvariant()
    };

    public static List<string> GetArguments(SqlDbType type, int precision, int scale, int length)
    {
        if (type is SqlDbType.Decimal)
        {
            return [$"{precision}", $"{scale}"];
        }

        if (ScaleTypes.Contains(type))
        {
            return [$"{scale}"];
        }

        if (!LengthTypes.Contains(type))
        {
            return [];
        }

        if (length < 0)
        {
            return ["max"];
        }

        return length == 0 ? [] : [$"{(WideTypes.Contains(type) ? length / 2 : length)}"];
    }

    public static string Format(SqlDbType type, int precision, int scale, int length)
    {
        var arguments = GetArguments(type, precision, scale, length);

        return arguments.Count == 0 ? GetName(type) : $"{GetName(type)}({string.Join(", ", arguments)})";
    }
}
