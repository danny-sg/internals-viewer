using System.Data;

namespace InternalsViewer.Execution.AccessPaths.Values;

public static class AccessValueConverter
{
    public static AccessValue ConvertTo(AccessValue value, SqlDbType dataType)
    {
        if (value.IsNull)
        {
            return AccessValue.FromNull(dataType);
        }

        if (value.DataType == dataType)
        {
            return value;
        }

        return dataType switch
        {
            SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt or SqlDbType.Bit
                => ToInteger(value, dataType),
            SqlDbType.Float or SqlDbType.Real
                => ToReal(value, dataType),
            SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney
                => ToDecimal(value, dataType),
            _ => value
        };
    }

    private static AccessValue ToInteger(AccessValue value, SqlDbType dataType)
        => value.Type switch
        {
            AccessValueType.Integer 
                => AccessValue.FromInteger(dataType, value.Numeric),
            AccessValueType.Real 
                => AccessValue.FromInteger(dataType, (long)value.Real),
            AccessValueType.Decimal
                => AccessValue.FromInteger(dataType, (long)value.ToDecimal()),
            _ => value
        };

    private static AccessValue ToReal(AccessValue value, SqlDbType dataType)
        => value.Type switch
        {
            AccessValueType.Integer 
                => AccessValue.FromReal(dataType, value.Numeric),
            AccessValueType.Real 
                => AccessValue.FromReal(dataType, value.Real),
            AccessValueType.Decimal 
                => AccessValue.FromReal(dataType, (double)value.ToDecimal()),
            _ => value
        };

    private static AccessValue ToDecimal(AccessValue value, SqlDbType dataType)
        => value.Type switch
        {
            AccessValueType.Integer 
                => AccessValue.FromDecimal(dataType, value.Numeric),
            AccessValueType.Real 
                => AccessValue.FromDecimal(dataType, (decimal)value.Real),
            AccessValueType.Decimal 
                => AccessValue.FromDecimal(dataType, value.ToDecimal()),
            _ => value
        };
}
