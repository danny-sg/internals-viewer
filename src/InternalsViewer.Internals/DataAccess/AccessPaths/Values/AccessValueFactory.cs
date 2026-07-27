using System.Data;
using System.Text;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// Creates <see cref="AccessValue"/> instances from record fields
/// </summary>
/// <remarks>
/// Text is always stored as UTF-16 regardless of the column type so that values from different string types compare consistently.
/// </remarks>
public static class AccessValueFactory
{
    public static AccessValue FromField(RecordField field)
    {
        var dataType = field.ColumnStructure.DataType;

        if (field.IsNull || field.Data.IsEmpty)
        {
            return AccessValue.FromNull(dataType);
        }

        return dataType switch
        {
            SqlDbType.BigInt => AccessValue.FromInteger(dataType, field.GetValue<long>()),
            SqlDbType.Int => AccessValue.FromInteger(dataType, field.GetValue<int>()),
            SqlDbType.SmallInt => AccessValue.FromInteger(dataType, field.GetValue<short>()),
            SqlDbType.TinyInt => AccessValue.FromInteger(dataType, field.GetValue<byte>()),
            SqlDbType.Bit => AccessValue.FromInteger(dataType, field.GetValue<bool>() ? 1 : 0),

            SqlDbType.DateTime
                or SqlDbType.SmallDateTime
                or SqlDbType.Date
                or SqlDbType.DateTime2 => AccessValue.FromInteger(dataType, field.GetValue<DateTime>().Ticks),

            SqlDbType.Time => AccessValue.FromInteger(dataType, field.GetValue<TimeSpan>().Ticks),

            SqlDbType.DateTimeOffset
                => AccessValue.FromInteger(dataType, field.GetValue<DateTimeOffset>().UtcTicks),

            SqlDbType.Float => AccessValue.FromReal(dataType, field.GetValue<double>()),
            SqlDbType.Real => AccessValue.FromReal(dataType, field.GetValue<float>()),

            SqlDbType.Decimal
                or SqlDbType.Money
                or SqlDbType.SmallMoney => AccessValue.FromDecimal(dataType, field.GetValue<decimal>()),

            SqlDbType.UniqueIdentifier
                => AccessValue.FromBytes(dataType, field.GetValue<Guid>().ToByteArray()),

            SqlDbType.Char
                or SqlDbType.VarChar
                or SqlDbType.Text
                or SqlDbType.NChar
                or SqlDbType.NVarChar
                or SqlDbType.NText => FromText(dataType, field.GetValue<string>()),

            _ => AccessValue.FromBytes(dataType, field.Data)
        };
    }

    /// <summary>
    /// Creates a value for a literal, used when building predicates
    /// </summary>
    public static AccessValue FromText(SqlDbType dataType, string? value)
    {
        if (value is null)
        {
            return AccessValue.FromNull(dataType);
        }

        return AccessValue.FromBytes(dataType, Encoding.Unicode.GetBytes(value));
    }
}
