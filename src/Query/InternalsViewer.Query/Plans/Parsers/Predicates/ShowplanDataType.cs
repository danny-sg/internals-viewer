using System.Data;

namespace InternalsViewer.Query.Plans.Parsers.Predicates;

public static class ShowplanDataType
{
    public static SqlDbType? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var text = name.Trim().Trim('[', ']');

        var bracket = text.IndexOf('(');

        if (bracket > 0)
        {
            text = text[..bracket];
        }

        return text.ToLowerInvariant() switch
        {
            "bigint" => SqlDbType.BigInt,
            "int" => SqlDbType.Int,
            "smallint" => SqlDbType.SmallInt,
            "tinyint" => SqlDbType.TinyInt,
            "bit" => SqlDbType.Bit,
            "decimal" or "numeric" => SqlDbType.Decimal,
            "money" => SqlDbType.Money,
            "smallmoney" => SqlDbType.SmallMoney,
            "float" => SqlDbType.Float,
            "real" => SqlDbType.Real,
            "date" => SqlDbType.Date,
            "datetime" => SqlDbType.DateTime,
            "datetime2" => SqlDbType.DateTime2,
            "datetimeoffset" => SqlDbType.DateTimeOffset,
            "smalldatetime" => SqlDbType.SmallDateTime,
            "time" => SqlDbType.Time,
            "char" => SqlDbType.Char,
            "varchar" => SqlDbType.VarChar,
            "text" => SqlDbType.Text,
            "nchar" => SqlDbType.NChar,
            "nvarchar" => SqlDbType.NVarChar,
            "ntext" => SqlDbType.NText,
            "binary" => SqlDbType.Binary,
            "varbinary" => SqlDbType.VarBinary,
            "image" => SqlDbType.Image,
            "uniqueidentifier" => SqlDbType.UniqueIdentifier,
            "xml" => SqlDbType.Xml,
            "sql_variant" => SqlDbType.Variant,
            _ => null
        };
    }
}
