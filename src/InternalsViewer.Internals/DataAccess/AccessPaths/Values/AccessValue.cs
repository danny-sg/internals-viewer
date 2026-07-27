using System.Data;
using System.Globalization;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// A single typed key or predicate value
/// </summary>
public readonly struct AccessValue : IEquatable<AccessValue>
{
    public static readonly AccessValue Null = new(SqlDbType.Variant, AccessValueKind.Null, 0, 0, default, null);

    private AccessValue(SqlDbType dataType,
                        AccessValueKind kind,
                        long numeric,
                        double real,
                        ReadOnlyMemory<byte> data,
                        string? columnName)
    {
        DataType = dataType;
        Kind = kind;
        Numeric = numeric;
        Real = real;
        Data = data;
        ColumnName = columnName;
    }

    public SqlDbType DataType { get; }

    public AccessValueKind Kind { get; }

    public long Numeric { get; }

    public double Real { get; }

    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// The column this value compares against, when known
    /// </summary>
    /// <remarks>
    /// Carrying the name on the value itself lets a predicate or seek bound be written back to text without a caller having to supply the
    /// index's key columns separately.
    /// </remarks>
    public string? ColumnName { get; }

    public bool IsNull => Kind == AccessValueKind.Null;

    public static bool operator ==(AccessValue left, AccessValue right) => left.Equals(right);

    public static bool operator !=(AccessValue left, AccessValue right) => !left.Equals(right);

    /// <summary>
    /// Creates a null value of a given type
    /// </summary>
    public static AccessValue FromNull(SqlDbType dataType)
    {
        return new AccessValue(dataType, AccessValueKind.Null, 0, 0, default, null);
    }

    /// <summary>
    /// Creates a signed integral value, also used for bit, date and time types
    /// </summary>
    public static AccessValue FromInteger(SqlDbType dataType, long value)
    {
        return new AccessValue(dataType, AccessValueKind.Integer, value, 0, default, null);
    }

    /// <summary>
    /// Creates a floating point value
    /// </summary>
    public static AccessValue FromReal(SqlDbType dataType, double value)
    {
        return new AccessValue(dataType, AccessValueKind.Real, 0, value, default, null);
    }

    /// <summary>
    /// Creates an exact numeric value, stored as its decimal bit representation
    /// </summary>
    public static AccessValue FromDecimal(SqlDbType dataType, decimal value)
    {
        var bits = decimal.GetBits(value);

        var data = new byte[sizeof(int) * 4];

        for (var index = 0; index < bits.Length; index++)
        {
            BitConverter.TryWriteBytes(data.AsSpan(index * sizeof(int)), bits[index]);
        }

        return new AccessValue(dataType, AccessValueKind.Decimal, 0, 0, data, null);
    }

    /// <summary>
    /// Creates a variable length value such as a string, binary or unique identifier
    /// </summary>
    public static AccessValue FromBytes(SqlDbType dataType, ReadOnlyMemory<byte> value)
    {
        return new AccessValue(dataType, AccessValueKind.Bytes, 0, 0, value, null);
    }

    /// <summary>
    /// Returns a copy of this value labelled with the column it compares against
    /// </summary>
    public AccessValue WithColumnName(string? columnName)
    {
        return new AccessValue(DataType, Kind, Numeric, Real, Data, columnName);
    }

    /// <summary>
    /// Gets the exact numeric payload previously stored by <see cref="FromDecimal"/>
    /// </summary>
    public decimal ToDecimal()
    {
        if (Kind != AccessValueKind.Decimal)
        {
            throw new InvalidOperationException($"Value of kind {Kind} is not an exact numeric.");
        }

        var span = Data.Span;

        Span<int> bits =
        [
            BitConverter.ToInt32(span),
            BitConverter.ToInt32(span[sizeof(int)..]),
            BitConverter.ToInt32(span[(sizeof(int) * 2)..]),
            BitConverter.ToInt32(span[(sizeof(int) * 3)..])
        ];

        return new decimal(bits);
    }

    public bool Equals(AccessValue other)
    {
        return AccessValueComparer.Compare(this, other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is AccessValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Kind switch
        {
            AccessValueKind.Null => 0,
            AccessValueKind.Integer
                or AccessValueKind.Real
                or AccessValueKind.Decimal 
                => GetNumericHashCode(),
            _ => GetBytesHashCode()
        };
    }

    public override string ToString()
    {
        return Kind switch
        {
            AccessValueKind.Null 
                => "NULL",
            AccessValueKind.Integer 
                => Numeric.ToString(),
            AccessValueKind.Real 
                => Real.ToString(CultureInfo.InvariantCulture),
            AccessValueKind.Decimal 
                => ToDecimal().ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToHexString(Data.Span)
        };
    }

    /// <summary>
    /// Hashes the three numeric kinds onto a single scale
    /// </summary>
    /// <remarks>
    /// Integer, real and exact numeric values compare equal across kinds, so they must hash
    /// identically. Values are narrowed to <see cref="double"/> because it is the only one of the
    /// three that every numeric kind converts to without overflowing.
    /// </remarks>
    private int GetNumericHashCode()
    {
        double value = Kind switch
        {
            AccessValueKind.Integer 
                => Numeric,
            AccessValueKind.Real 
                => Real,
            _ => (double)ToDecimal()
        };

        return value.GetHashCode();
    }

    private int GetBytesHashCode()
    {
        HashCode hash = default;

        hash.AddBytes(Data.Span);

        return hash.ToHashCode();
    }
}
