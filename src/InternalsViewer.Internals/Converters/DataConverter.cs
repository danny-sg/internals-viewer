using System.Buffers.Binary;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Text;
using InternalsViewer.Internals.Helpers;
using static System.Text.RegularExpressions.Regex;

namespace InternalsViewer.Internals.Converters;

/// <summary>
/// Utility methods for converting from raw data to SQL Server/.NET data types
/// </summary>
public static class DataConverter
{
    /// <summary>
    /// Converts a binary GUID to string
    /// </summary>
    /// <param name="data">The data.</param>
    public static Guid BinaryToGuid(byte[]? data)
    {
        if (data == null)
        {
            return Guid.Empty;
        }

        if (data.Length != 16)
        {
            throw new ArgumentException("Invalid GUID");
        }

        return new Guid(data);
    }

    public static List<string> DecodeDataString(string data)
    {
        var decodedData = new List<string>();

        var binaryData = new byte[data.Length / 2];

        for (var i = 0; i < binaryData.Length; i++)
        {
            binaryData[i] = byte.Parse(data.AsSpan(i * 2, 2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        switch (binaryData.Length)
        {
            case 1:
                decodedData.Add(DataString(binaryData, SqlDbType.TinyInt));
                break;
            case 2:
                decodedData.Add(DataString(binaryData, SqlDbType.SmallInt));
                break;
            case 4:
                decodedData.Add(DataString(binaryData, SqlDbType.Int));
                break;
            case 8:
                decodedData.Add(DataString(binaryData, SqlDbType.BigInt));
                break;
        }

        if (binaryData.Length == 1)
        {
            var b = binaryData[0];
            var binaryString = string.Create(10, b, static (span, v) =>
            {
                span[0] = 'b'; span[1] = 'i'; span[2] = 'n'; span[3] = 'a';
                span[4] = 'r'; span[5] = 'y'; span[6] = ':'; span[7] = ' ';
                for (var i = 0; i < 8; i++)
                    span[9 - i] = (v & (1 << i)) != 0 ? '1' : '0';
            });
            decodedData.Add(binaryString);
        }

        var varcharData = DataString(binaryData, SqlDbType.VarChar);

        if (!string.IsNullOrEmpty(varcharData))
        {
            decodedData.Add(DataString(binaryData, SqlDbType.VarChar));
        }

        return decodedData;
    }

    /// <summary>
    /// Converts a byte array to a value
    /// </summary>
    public static string BinaryToString(ReadOnlySpan<byte> data,
                                        SqlDbType sqlType,
                                        byte precision = 0,
                                        byte scale = 0,
                                        short bitPosition = 0)
    {
        if (data is not { Length: not 0 })
        {
            return string.Empty;
        }

        try
        {
            switch (sqlType)
            {
                // Number types
                case SqlDbType.TinyInt:
                    // tinyint is one byte
                    return ((int)data[0]).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.SmallInt:
                    // smallint is a two byte (16 bit) integer    
                    return BinaryPrimitives.ReadInt16LittleEndian(data).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.Int:
                    // int is a four byte (32 bit) integer  
                    return BinaryPrimitives.ReadInt32LittleEndian(data).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.BigInt:
                    // bigint is an eight byte (64 bit) integer  
                    return BinaryPrimitives.ReadInt64LittleEndian(data).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.Decimal:
                    return DecodeDecimal(data, precision, scale).ToString();

                case SqlDbType.SmallMoney:
                    // smallmoney is a four byte (32 bit) integer with a precision of 4 decimal places, i.e. divided by 10,000
                    return (BinaryPrimitives.ReadInt32LittleEndian(data) / 10000M).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Money:
                    // money is an eight byte (64 bit) integer with a precision of 4 decimal places, i.e. divided by 10,000
                    return (BinaryPrimitives.ReadInt64LittleEndian(data) / 10000M).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Real:
                    // Real is a four byte (32 bit) floating point number
                    return BinaryPrimitives.ReadSingleLittleEndian(data).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Float:
                    // Float is an eight byte (64 bit) floating point number
                    return BinaryPrimitives.ReadDoubleLittleEndian(data).ToString(CultureInfo.InvariantCulture);

                // String types
                case SqlDbType.Char:
                case SqlDbType.VarChar:
                    return Replace(Encoding.UTF8.GetString(data), @"[^\t -~]", string.Empty);

                case SqlDbType.NChar:
                case SqlDbType.NVarChar:
                    return Encoding.Unicode.GetString(data);

                // Date types
                case SqlDbType.DateTime:
                    return DateTimeConverters.DecodeDateTime(data).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.SmallDateTime:
                    return DateTimeConverters.DecodeSmallDateTime(data).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Date:
                    return DateTimeConverters.DecodeDate(data).ToShortDateString();

                case SqlDbType.Time:
                    {
                        var format = scale == 0 ? @"hh\:mm\:ss" : @"hh\:mm\:ss\." + new string('f', scale);

                        return DateTimeConverters.DecodeTime(data, scale).ToString(format);
                    }

                case SqlDbType.DateTime2:
                    {
                        var format = scale == 0
                                     ? @"yyyy-MM-dd HH\:mm\:ss"
                                     : @"yyyy-MM-dd HH\:mm\:ss\." + new string('f', scale);

                        return DateTimeConverters.DecodeDateTime2(data, scale).ToString(format);
                    }

                case SqlDbType.DateTimeOffset:
                    return DateTimeConverters.DecodeDateTimeOffset(data, scale);

                // Binary types
                case SqlDbType.VarBinary:
                case SqlDbType.Binary:
                    return "0x" + Convert.ToHexString(data);

                case SqlDbType.UniqueIdentifier:
                    return new Guid(data).ToString().ToUpper();

                // SQL Variant
                case SqlDbType.Variant:
                    return VariantBinaryToString(data);

                case SqlDbType.Bit:
                    return GetBit(data[0], bitPosition);

                default:
                    return string.Format(CultureInfo.CurrentCulture,
                                         "not yet supported ({0:G})",
                                         sqlType);
            }
        }
        catch (Exception ex)
        {
            return $"Error converting data - {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the value of a byte array in terms of a typed object (T) based on the SQL type
    /// </summary>
    public static T? GetValue<T>(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        return (T?)GetValue(data, sqlType, precision, scale);
    }

    public static T? GetValue<T>(ReadOnlySpan<byte> data, SqlDbType sqlType, byte precision, byte scale)
    {
        return (T?)GetValue(data, sqlType, precision, scale);
    }

    /// <summary>
    /// Gets the value of a byte array in terms of an untyped object based on the SQL type
    /// </summary>
    public static object? GetValue(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        if (data == null)
        {
            return null;
        }

        return GetValue(new ReadOnlySpan<byte>(data), sqlType, precision, scale);
    }

    /// <summary>
    /// Gets the value of a span in terms of an untyped object based on the SQL type
    /// </summary>
    public static object? GetValue(ReadOnlySpan<byte> data, SqlDbType sqlType, byte precision, byte scale)
    {
        try
        {
            return sqlType switch
            {
                SqlDbType.BigInt => BinaryPrimitives.ReadInt64LittleEndian(data),
                SqlDbType.Int => BinaryPrimitives.ReadInt32LittleEndian(data),
                SqlDbType.TinyInt => data[0],
                SqlDbType.SmallInt => BinaryPrimitives.ReadInt16LittleEndian(data),
                SqlDbType.Char => Encoding.UTF8.GetString(data),
                SqlDbType.VarChar => Encoding.UTF8.GetString(data),
                SqlDbType.NChar => Encoding.Unicode.GetString(data),
                SqlDbType.NVarChar => Encoding.Unicode.GetString(data),
                SqlDbType.DateTime => DateTimeConverters.DecodeDateTime(data),
                SqlDbType.SmallDateTime => DateTimeConverters.DecodeSmallDateTime(data),
                SqlDbType.VarBinary => data.ToArray(),
                SqlDbType.Binary => data.ToArray(),
                SqlDbType.UniqueIdentifier => new Guid(data),
                SqlDbType.Decimal => DecodeDecimal(data, precision, scale),
                SqlDbType.Money => BinaryPrimitives.ReadInt64LittleEndian(data) / 10000M,
                SqlDbType.SmallMoney => BinaryPrimitives.ReadInt32LittleEndian(data) / 10000M,
                SqlDbType.Real => BinaryPrimitives.ReadSingleLittleEndian(data),
                SqlDbType.Float => BinaryPrimitives.ReadDoubleLittleEndian(data),
                SqlDbType.Variant => VariantBinaryToString(data),
                SqlDbType.Date => DateTimeConverters.DecodeDate(data),
                SqlDbType.Time => DateTimeConverters.DecodeTime(data, scale),
                SqlDbType.DateTime2 => DateTimeConverters.DecodeDateTime2(data, scale),
                SqlDbType.DateTimeOffset => DateTimeConverters.DecodeDateTimeOffset(data, scale),
                _ => string.Format(CultureInfo.CurrentCulture, "not yet supported ({0:G})", sqlType)
            };
        }
        catch
        {
            return "Error converting data";
        }
    }

    /// <summary>
    /// Gets a bit from a byte
    /// </summary>
    /// <remarks>
    /// Bit Position is the zero based bit position of the field. Bits are stored in bytes, up to 8 per byte
    /// </remarks>
    private static string GetBit(byte data, short bitPosition)
    {
        return ((data & (1 << bitPosition)) != 0).ToString();
    }

    /// <summary>
    /// Returns a string representation of a variant data type
    /// </summary>
    private static string VariantBinaryToString(ReadOnlySpan<byte> data)
    {
        var variantType = data[0];
        var offset = 2;
        byte precision = 0;
        byte scale = 0;

        switch (SqlTypeHelpers.ToSqlType(variantType))
        {
            case SqlDbType.Decimal:

                precision = data[2];
                scale = data[3];
                offset += 2;
                break;

            case SqlDbType.Char:
            case SqlDbType.VarChar:
            case SqlDbType.NChar:
            case SqlDbType.NVarChar:

                offset += 6;
                break;

            case SqlDbType.VarBinary:
            case SqlDbType.Binary:

                offset += 2;
                break;

            case SqlDbType.Time:
            case SqlDbType.DateTime2:
            case SqlDbType.DateTimeOffset:
                scale = data[2];
                offset += 1;
                break;
        }

        return BinaryToString(data[offset..], SqlTypeHelpers.ToSqlType(variantType), precision, scale);
    }

    /// <summary>
    /// Decodes the DECIMAL/NUMERIC data type
    /// </summary>
    /// <remarks>
    /// Precision determines the maximum number of digits that can be stored
    ///
    /// Scale determines the number of digits that can be stored to the right of the decimal point
    /// 
    /// This uses System.Data.SqlTypes.SqlDecimal to get the value
    /// </remarks>
    private static SqlDecimal DecodeDecimal(ReadOnlySpan<byte> data, byte precision, byte scale)
    {
        var positive = data[0] == 1;
        var bits = new int[4];
        var length = (data.Length - 1) >> 2;

        for (var index = 0; index < length; index++)
        {
            bits[index] = BinaryPrimitives.ReadInt32LittleEndian(data[(1 + index * 4)..]);
        }

        return new SqlDecimal(precision, scale, positive, bits);
    }

    private static string DataString(byte[] data, SqlDbType sqlType)
    {
        var stringBuilder = new StringBuilder(sqlType.ToString().ToLower(CultureInfo.CurrentCulture));

        stringBuilder.Append(": ");
        try
        {
            stringBuilder.Append(BinaryToString(data, sqlType));
        }
        catch
        {
            stringBuilder.Append("(Error)");
        }

        return stringBuilder.ToString();
    }
}