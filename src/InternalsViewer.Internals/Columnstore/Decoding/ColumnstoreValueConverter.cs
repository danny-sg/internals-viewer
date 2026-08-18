using System.Buffers.Binary;
using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Converts a decoded segment value to the type the column holds
/// </summary>
/// <remarks>
/// A segment data id, once base and magnitude are applied, is the same integer a row store record holds for the
/// column, so the row store converter does the type mapping.
/// </remarks>
public static class ColumnstoreValueConverter
{
    public static object? Convert(object? decoded, ColumnStructure? structure)
    {
        if (decoded is null || structure is null)
        {
            return decoded;
        }

        if (decoded is byte[] bytes)
        {
            return DataConverter.GetValue(bytes, structure.DataType, structure.Precision, structure.Scale);
        }

        if (decoded is not (long or decimal))
        {
            return decoded;
        }

        var storage = decoded is decimal scaled ? (long)scaled : (long)decoded;

        return ConvertStorage(storage, structure);
    }

    private static object? ConvertStorage(long storage, ColumnStructure structure)
    {
        switch (structure.DataType)
        {
            case SqlDbType.Bit:
                return storage != 0;

            // Segments hold real as a double, so the narrowing happens after the bit pattern is read
            case SqlDbType.Real:
                return (float)BitConverter.Int64BitsToDouble(storage);

            // The scaled integer arrives whole rather than in the sign and magnitude layout a record holds
            case SqlDbType.Decimal:
                return storage / Power10(structure.Scale);
        }

        Span<byte> buffer = stackalloc byte[8];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, storage);

        var length = GetStorageLength(structure);

        return DataConverter.GetValue(buffer[..length], structure.DataType, structure.Precision, structure.Scale);
    }

    private static decimal Power10(byte scale)
    {
        var value = 1M;

        for (var i = 0; i < scale; i++)
        {
            value *= 10;
        }

        return value;
    }

    private static int GetStorageLength(ColumnStructure structure) => structure.DataType switch
    {
        SqlDbType.TinyInt => 1,
        SqlDbType.SmallInt => 2,
        SqlDbType.Int or SqlDbType.SmallMoney or SqlDbType.SmallDateTime => 4,
        SqlDbType.BigInt or SqlDbType.Float or SqlDbType.Money or SqlDbType.DateTime => 8,
        SqlDbType.Date => 3,
        _ => structure.DataLength is > 0 and <= 8 ? structure.DataLength : 8
    };
}
