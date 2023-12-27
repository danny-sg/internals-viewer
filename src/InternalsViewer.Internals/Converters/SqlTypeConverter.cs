using System.Data;

namespace InternalsViewer.Internals.Converters;

public class SqlTypeConverter
{
    private static readonly Dictionary<byte, SqlDbType> Types = new()
    {
        { 34, SqlDbType.Image },
        { 35, SqlDbType.Text },
        { 36, SqlDbType.UniqueIdentifier },
        { 40, SqlDbType.Date },
        { 41, SqlDbType.Time },
        { 42, SqlDbType.DateTime2 },
        { 43, SqlDbType.DateTimeOffset },
        { 48, SqlDbType.TinyInt },
        { 52, SqlDbType.SmallInt },
        { 56, SqlDbType.Int },
        { 58, SqlDbType.SmallDateTime },
        { 59, SqlDbType.Real },
        { 60, SqlDbType.Money },
        { 61, SqlDbType.DateTime },
        { 62, SqlDbType.Float },
        { 98, SqlDbType.Variant },
        { 99, SqlDbType.NText },
        { 104, SqlDbType.Bit },
        { 106, SqlDbType.Decimal },
        { 108, SqlDbType.Decimal },
        { 122, SqlDbType.SmallMoney },
        { 127, SqlDbType.BigInt },
        { 165, SqlDbType.VarBinary },
        { 167, SqlDbType.VarChar },
        { 173, SqlDbType.Binary },
        { 175, SqlDbType.Char },
        { 189, SqlDbType.Timestamp },
        { 231, SqlDbType.NVarChar },
        { 239, SqlDbType.NChar },
        { 241, SqlDbType.Xml }
    };

    public static SqlDbType ToSqlType(byte type)
    {
        return Types[type];
    }
}
