using System.Data;
using System.Globalization;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.Records;

/// <summary>
/// Computed/Derived Record Field
/// </summary>
public sealed class ComputedField : RecordField
{
    public ComputedField(string name, AccessValue value)
        : base(new ColumnStructure { ColumnName = name, DataType = value.DataType })
    {
        ComputedValue = value;

        IsNull = value.IsNull;

        Data = value.Data;

        Length = (ushort)value.Data.Length;
    }

    public AccessValue ComputedValue { get; }

    public override string Value => ToText(ComputedValue);

    public override T? GetValue<T>()
        where T : default
    {
        var value = Convert(ComputedValue, typeof(T));

        return value is T typed ? typed : default;
    }

    private static object? Convert(AccessValue value, Type type)
    {
        if (value.IsNull)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return ToText(value);
        }

        return value.Type switch
        {
            AccessValueType.Integer 
                => FromNumber(value.Numeric, type),
            AccessValueType.Real 
                => FromNumber(value.Real, type),
            AccessValueType.Decimal 
                => FromNumber(value.ToDecimal(), type),
            _ => FromBytes(value, type)
        };
    }

    private static object? FromNumber(object number, Type type)
    {
        if (type == typeof(bool))
        {
            return System.Convert.ToInt64(number, CultureInfo.InvariantCulture) != 0;
        }

        if (type == typeof(DateTime))
        {
            return new DateTime(System.Convert.ToInt64(number, CultureInfo.InvariantCulture));
        }

        if (type == typeof(TimeSpan))
        {
            return new TimeSpan(System.Convert.ToInt64(number, CultureInfo.InvariantCulture));
        }

        return System.Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
    }

    private static object? FromBytes(AccessValue value, Type type)
    {
        if (type == typeof(Guid) && value.Data.Length == 16)
        {
            return new Guid(value.Data.Span);
        }

        return type == typeof(byte[]) ? value.Data.ToArray() : null;
    }

    private static string ToText(AccessValue value)
    {
        if (value.IsNull)
        {
            return string.Empty;
        }

        if (AccessValueFormatter.TryFormatTemporal(value, out var temporal))
        {
            return temporal;
        }

        return value.Type switch
        {
            AccessValueType.Integer 
                => value.Numeric.ToString(CultureInfo.InvariantCulture),
            AccessValueType.Real 
                => value.Real.ToString("R", CultureInfo.InvariantCulture),
            AccessValueType.Decimal 
                => value.ToDecimal().ToString(CultureInfo.InvariantCulture),
            _ => BytesToText(value)
        };
    }

    private static string BytesToText(AccessValue value)
        => value.DataType switch
        {
            SqlDbType.Char or SqlDbType.VarChar or SqlDbType.Text
                => Encoding.Latin1.GetString(value.Data.Span),
            SqlDbType.NChar or SqlDbType.NVarChar or SqlDbType.NText
                => Encoding.Unicode.GetString(value.Data.Span),
            SqlDbType.UniqueIdentifier when value.Data.Length == 16
                => new Guid(value.Data.Span).ToString(),
            _ => value.Data.IsEmpty ? string.Empty : System.Convert.ToHexString(value.Data.Span)
        };
}
