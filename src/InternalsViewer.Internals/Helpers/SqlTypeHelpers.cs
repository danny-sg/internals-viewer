using System.Data;

namespace InternalsViewer.Internals.Helpers;

public class SqlTypeHelpers
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

    public static bool IsVariableLength(SqlDbType type)
    {
        return !IsFixedLength(type);
    }

    public static bool IsFixedLength(SqlDbType type)
    {
        switch (type)
        {
            case SqlDbType.Int:
            case SqlDbType.SmallInt:
            case SqlDbType.TinyInt:
            case SqlDbType.BigInt:
            case SqlDbType.Bit:
            case SqlDbType.Date:
            case SqlDbType.DateTime:
            case SqlDbType.DateTime2:
            case SqlDbType.DateTimeOffset:
            case SqlDbType.SmallDateTime:
            case SqlDbType.Time:
            case SqlDbType.UniqueIdentifier:
            case SqlDbType.SmallMoney:
            case SqlDbType.Money:
            case SqlDbType.Real:
            case SqlDbType.Float:
            case SqlDbType.Decimal:
            case SqlDbType.Timestamp:
                return true;
            case SqlDbType.Image:
            case SqlDbType.NText:
            case SqlDbType.Text:
            case SqlDbType.VarBinary:
            case SqlDbType.VarChar:
            case SqlDbType.Binary:
            case SqlDbType.Char:
            case SqlDbType.NChar:
            case SqlDbType.NVarChar:
            case SqlDbType.Xml:
            case SqlDbType.Variant:
            default:
                return false;
        }
    }
}
