using System.Data;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// Converts a decoded <see cref="RecordField"/> into a labelled <see cref="AccessValue"/>
/// </summary>
/// <remarks>
/// Field bytes are already positioned and typed by the record loader, so this factory only needs to
/// classify the data type into the storage kind an <see cref="AccessValue"/> understands.
/// </remarks>
public static class AccessValueFieldFactory
{
    public static AccessValue Create(RecordField field)
    {
        var dataType = field.ColumnStructure.DataType;
        var columnName = field.ColumnStructure.ColumnName;

        if (field.IsNull)
        {
            return AccessValue.FromNull(dataType).WithColumnName(columnName);
        }

        return dataType switch
        {
            SqlDbType.TinyInt
                or SqlDbType.SmallInt
                or SqlDbType.Int
                or SqlDbType.BigInt
                or SqlDbType.Bit
                or SqlDbType.Date
                or SqlDbType.DateTime
                or SqlDbType.SmallDateTime
                or SqlDbType.DateTime2
                => AccessValue.FromInteger(dataType, field.GetValue<long>()).WithColumnName(columnName),
            SqlDbType.Real
                or SqlDbType.Float
                => AccessValue.FromReal(dataType, field.GetValue<double>()).WithColumnName(columnName),
            SqlDbType.Decimal
                or SqlDbType.Money
                or SqlDbType.SmallMoney
                => AccessValue.FromDecimal(dataType, field.GetValue<decimal>()).WithColumnName(columnName),
            _ => AccessValue.FromBytes(dataType, field.Data).WithColumnName(columnName)
        };
    }
}