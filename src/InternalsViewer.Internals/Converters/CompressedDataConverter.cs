using System.Buffers.Binary;
using System.Data;
using System.Globalization;

namespace InternalsViewer.Internals.Converters;

public static class CompressedDataConverter
{
    /// <summary>
    /// The value a column descriptor of one represents, where the column takes no bytes at all
    /// </summary>
    /// <remarks>
    /// Compression stores zero and the empty string as nothing, which is distinct from null and has to read as the value rather than as
    /// a blank.
    /// </remarks>
    private static string ZeroLengthValue(SqlDbType sqlType)
        => sqlType switch
        {
            SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt => "0",
            SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney => "0",
            SqlDbType.Float or SqlDbType.Real => "0",
            SqlDbType.Bit => "0",
            _ => string.Empty
        };

    public static string CompressedBinaryToString(ReadOnlySpan<byte> data, SqlDbType sqlType, byte precision, byte scale)
    {
        if (data.IsEmpty)
        {
            return ZeroLengthValue(sqlType);
        }

        try
        {
            var unsigned = (data[0] & 0x80) != 0;

            switch (sqlType)
            {
                case SqlDbType.BigInt:
                    return DecodeBigInt(data, unsigned);

                case SqlDbType.TinyInt:
                    return DataConverter.BinaryToString(data, sqlType, precision, scale);

                case SqlDbType.SmallInt:
                    return DecodeSmallInt(data, unsigned);

                case SqlDbType.Int:
                    return DecodeInt(data, unsigned);

                case SqlDbType.Money:
                    return DataConverter.BinaryToString(DecodeInt(data, unsigned, 4), sqlType, precision, scale);

                case SqlDbType.SmallMoney:
                    return DataConverter.BinaryToString(DecodeInt(data, unsigned, 8), sqlType, precision, scale);

                case SqlDbType.NChar:
                case SqlDbType.NVarChar:
                    return DataConverter.BinaryToString(data, SqlDbType.VarChar, precision, scale);

                case SqlDbType.DateTime:
                    return DecodeDateTime(data, unsigned);

                case SqlDbType.SmallDateTime:
                    return DecodeSmallDateTime(data);
                default:
                    return DataConverter.BinaryToString(data, sqlType, precision, scale);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Expands compressed data to the fixed width representation the type normally uses
    /// </summary>
    /// <remarks>
    /// Row compression trims leading bytes and stores integers big-endian, so compressed data
    /// cannot be handed straight to <see cref="DataConverter"/>. This restores the full width
    /// little-endian form and reports the type to decode it as, which is not always the column
    /// type - compressed unicode is stored as single byte characters.
    /// </remarks>
    public static (byte[] Data, SqlDbType DataType) Expand(ReadOnlySpan<byte> data, SqlDbType sqlType)
    {
        if (data.IsEmpty)
        {
            return ([], sqlType);
        }

        var unsigned = (data[0] & 0x80) != 0;

        return sqlType switch
        {
            SqlDbType.BigInt => ([.. DecodeInt(data, unsigned, 8)], sqlType),
            SqlDbType.SmallInt => ([.. DecodeInt(data, unsigned, 2)], sqlType),
            SqlDbType.Int => ([.. DecodeInt(data, unsigned, 4)], sqlType),
            SqlDbType.Money => ([.. DecodeInt(data, unsigned, 4)], sqlType),
            SqlDbType.SmallMoney => ([.. DecodeInt(data, unsigned, 8)], sqlType),
            SqlDbType.NChar or SqlDbType.NVarChar => ([.. data], SqlDbType.VarChar),
            SqlDbType.DateTime => (ExpandDateTime(data, unsigned), SqlDbType.DateTime),
            SqlDbType.SmallDateTime => (ExpandSmallDateTime(data), SqlDbType.SmallDateTime),
            _ => ([.. data], sqlType)
        };
    }

    public static int DecodeInternalInt(byte[] data, int startPosition)
    {
        if ((data[startPosition] & 0x80) != 0 && data.Length > 1)
        {
            return (data[startPosition] ^ 0x80) | (data[startPosition + 1] << 8);
        }

        return data[startPosition];
    }

    private static string DecodeBigInt(ReadOnlySpan<byte> data, bool unsigned)
    {
        var decoded = DecodeInt(data, unsigned, 8);

        return unsigned
            ? BinaryPrimitives.ReadUInt64LittleEndian(decoded).ToString(CultureInfo.CurrentCulture)
            : BinaryPrimitives.ReadInt64LittleEndian(decoded).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeSmallInt(ReadOnlySpan<byte> data, bool unsigned)
    {
        var decoded = DecodeInt(data, unsigned, 2);

        return unsigned
            ? BinaryPrimitives.ReadUInt16LittleEndian(decoded).ToString(CultureInfo.CurrentCulture)
            : BinaryPrimitives.ReadInt16LittleEndian(decoded).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeInt(ReadOnlySpan<byte> data, bool unsigned)
    {
        var decoded = DecodeInt(data, unsigned, 4);

        return unsigned
            ? BinaryPrimitives.ReadUInt32LittleEndian(decoded).ToString(CultureInfo.CurrentCulture)
            : BinaryPrimitives.ReadInt32LittleEndian(decoded).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeSmallDateTime(ReadOnlySpan<byte> data)
    {
        return DataConverter.BinaryToString(ExpandSmallDateTime(data), SqlDbType.SmallDateTime);
    }

    private static byte[] ExpandSmallDateTime(ReadOnlySpan<byte> data)
    {
        if (data.Length == 2)
        {
            var expandedDate = new byte[4];

            data.CopyTo(expandedDate.AsSpan(2));

            return expandedDate;
        }

        return [.. data];
    }

    private static string DecodeDateTime(ReadOnlySpan<byte> data, bool unsigned)
    {
        return DataConverter.BinaryToString(ExpandDateTime(data, unsigned), SqlDbType.DateTime);
    }

    private static byte[] ExpandDateTime(ReadOnlySpan<byte> data, bool unsigned)
    {
        var expandedDateTime = new byte[8];

        if (data.Length < 5)
        {
            DecodeInt(data, unsigned, 4).CopyTo(expandedDateTime);
        }
        else
        {
            var timePart = data[^4..];
            var datePart = data[..^4];

            DecodeInt(timePart, (timePart[0] & 0x80) == 0x80, 4).CopyTo(expandedDateTime);

            Span<byte> dateDecoded = stackalloc byte[datePart.Length];
            datePart.CopyTo(dateDecoded);

            if (!unsigned)
            {
                dateDecoded[0] ^= 0x80;
            }

            DecodeInt(dateDecoded, unsigned, 4).CopyTo(expandedDateTime.AsSpan(4));
        }

        return expandedDateTime;
    }

    /// <summary>
    /// Decodes a compressed big-endian integer into a little-endian buffer of the requested size.
    /// </summary>
    /// <remarks>
    /// SQL Server row compression stores integers big-endian with the sign bit in the high byte masked.
    /// 
    /// Unsigned values have 0x80 XOR'd into the most-significant byte. Signed values are sign-extended with 0xFF.
    /// </remarks>
    private static Span<byte> DecodeInt(ReadOnlySpan<byte> data, bool unsigned, int size)
    {
        var returnData = new byte[size];

        if (!unsigned)
        {
            returnData.AsSpan().Fill(0xFF);
        }

        // Reverse copy: data is big-endian, returnData should be little-endian
        for (var i = 0; i < data.Length; i++)
        {
            returnData[data.Length - 1 - i] = data[i];
        }

        if (unsigned)
        {
            // XOR was originally on data[0] (MSB); after reversal that byte is at index data.Length-1
            returnData[data.Length - 1] ^= 0x80;
        }

        return returnData;
    }
}