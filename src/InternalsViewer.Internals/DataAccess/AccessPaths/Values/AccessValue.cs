using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// A single typed key or predicate value
/// </summary>
public readonly struct AccessValue : IEquatable<AccessValue>
{
    public static readonly AccessValue Null = new(SqlDbType.Variant, AccessValueType.Null, 0, 0, default, null);

    private AccessValue(SqlDbType dataType,
                        AccessValueType type,
                        long numeric,
                        double real,
                        ReadOnlyMemory<byte> data,
                        string? columnName)
    {
        DataType = dataType;
        Type = type;
        Numeric = numeric;
        Real = real;
        Data = data;
        ColumnName = columnName;
    }

    public SqlDbType DataType { get; }

    public AccessValueType Type { get; }

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

    public bool IsNull => Type == AccessValueType.Null;

    public static bool operator ==(AccessValue left, AccessValue right) => left.Equals(right);

    public static bool operator !=(AccessValue left, AccessValue right) => !left.Equals(right);

    /// <summary>
    /// Creates a null value of a given type
    /// </summary>
    public static AccessValue FromNull(SqlDbType dataType)
    {
        return new AccessValue(dataType, AccessValueType.Null, 0, 0, default, null);
    }

    /// <summary>
    /// Creates a signed integral value, also used for bit, date and time types
    /// </summary>
    public static AccessValue FromInteger(SqlDbType dataType, long value)
    {
        return new AccessValue(dataType, AccessValueType.Integer, value, 0, default, null);
    }

    /// <summary>
    /// Creates a floating point value
    /// </summary>
    public static AccessValue FromReal(SqlDbType dataType, double value)
    {
        return new AccessValue(dataType, AccessValueType.Real, 0, value, default, null);
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

        return new AccessValue(dataType, AccessValueType.Decimal, 0, 0, data, null);
    }

    /// <summary>
    /// Creates a variable length value such as a string, binary or unique identifier
    /// </summary>
    public static AccessValue FromBytes(SqlDbType dataType, ReadOnlyMemory<byte> value)
    {
        return new AccessValue(dataType, AccessValueType.Bytes, 0, 0, value, null);
    }

    /// <summary>
    /// Returns a copy of this value labelled with the column it compares against
    /// </summary>
    public AccessValue WithColumnName(string? columnName)
    {
        return new AccessValue(DataType, Type, Numeric, Real, Data, columnName);
    }

    /// <summary>
    /// Gets the exact numeric payload previously stored by <see cref="FromDecimal"/>
    /// </summary>
    public decimal ToDecimal()
    {
        if (Type != AccessValueType.Decimal)
        {
            throw new InvalidOperationException($"Value of type {Type} is not an exact numeric.");
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
        return Type switch
        {
            AccessValueType.Null => 0,
            AccessValueType.Integer
                or AccessValueType.Real
                or AccessValueType.Decimal 
                => GetNumericHashCode(),
            _ => GetBytesHashCode()
        };
    }

    public override string ToString()
    {
        return Type switch
        {
            AccessValueType.Null 
                => "NULL",
            AccessValueType.Integer 
                => Numeric.ToString(),
            AccessValueType.Real 
                => Real.ToString(CultureInfo.InvariantCulture),
            AccessValueType.Decimal 
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
    /// three that every numeric type converts to without overflowing.
    /// </remarks>
    private int GetNumericHashCode()
    {
        double value = Type switch
        {
            AccessValueType.Integer 
                => Numeric,
            AccessValueType.Real 
                => Real,
            _ => (double)ToDecimal()
        };

        return value.GetHashCode();
    }

    private int GetBytesHashCode()
    {
        HashCode hash = default;

        if (AccessValueComparer.IsCharacterType(DataType))
        {
            if (AccessValueComparer.IsWideCharacterType(DataType))
            {
                foreach (var character in MemoryMarshal.Cast<byte, char>(Data.Span).TrimEnd(' '))
                {
                    hash.Add(char.ToUpperInvariant(character));
                }
            }
            else
            {
                var span = Data.Span;

                var length = span.Length;

                while (length > 0 && span[length - 1] == 0x20)
                {
                    length--;
                }

                for (var index = 0; index < length; index++)
                {
                    hash.Add(char.ToUpperInvariant((char)span[index]));
                }
            }

            return hash.ToHashCode();
        }

        hash.AddBytes(Data.Span);

        return hash.ToHashCode();
    }
}
