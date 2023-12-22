﻿using System.Collections;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Text;
using static System.Text.RegularExpressions.Regex;

namespace InternalsViewer.Internals.Converters;

/// <summary>
/// Utility methods for converting from raw data to SQL Server/.NET data types
/// </summary>
public static class DataConverter
{
    private static readonly char[] HexDigits =
        {
            '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

    /// <summary>
    /// Returns a hex string for a given array of bytes
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns></returns>
    public static string ToHexString(byte[]? bytes)
    {
        if (bytes == null)
        {
            return string.Empty;
        }

        var chars = new char[bytes.Length * 2];

        for (var i = 0; i < bytes.Length; i++)
        {
            int b = bytes[i];

            chars[i * 2] = HexDigits[b >> 4];
            chars[i * 2 + 1] = HexDigits[b & 0xF];
        }

        return new string(chars);
    }

    /// <summary>
    /// Returns a hex string for a given byte
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns></returns>
    public static string ToHexString(byte bytes)
    {
        return ToHexString(new byte[1] { bytes });
    }

    /// <summary>
    /// Converts a bit at a given index of a byte array
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="index">The index.</param>
    /// <returns></returns>
    public static string BinaryToString(byte[] data, int index)
    {
        return new BitArray(data).Get(index).ToString();
    }

    /// <summary>
    /// Converts a binary GUID to string
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns></returns>
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

    /// <summary>
    /// Converts a byte array to a value
    /// </summary>
    /// <param name="data">The data</param>
    /// <param name="sqlType">SQL Server type</param>
    /// <param name="precision">The number precision (if decimal type)</param>
    /// <param name="scale">The number scale (if decimal type)</param>
    public static string BinaryToString(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        if (data == null)
        {
            return string.Empty;
        }

        try
        {
            switch (sqlType)
            {
                case SqlDbType.BigInt:
                    return BitConverter.ToInt64(data, 0).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.Int:
                    return BitConverter.ToInt32(data, 0).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.TinyInt:
                    return ((int)data[0]).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.SmallInt:
                    return BitConverter.ToInt16(data, 0).ToString(CultureInfo.CurrentCulture);

                case SqlDbType.Char:
                case SqlDbType.VarChar:
                    return Replace(Encoding.UTF8.GetString(data), @"[^\t -~]", string.Empty);

                case SqlDbType.NChar:
                case SqlDbType.NVarChar:
                    return Encoding.Unicode.GetString(data);

                case SqlDbType.DateTime:
                    return DateTimeConverters.DecodeDateTime(data).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.SmallDateTime:
                    return DateTimeConverters.DecodeSmallDateTime(data).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.VarBinary:
                case SqlDbType.Binary:
                    return "0x" + ToHexString(data);

                case SqlDbType.UniqueIdentifier:
                    return BinaryToGuid(data).ToString();

                case SqlDbType.Decimal:
                    return DecodeDecimal(data, precision, scale).ToString();

                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    return (BitConverter.ToInt64(data, 0) / 10000.0).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Real:
                    return BitConverter.ToSingle(data, 0).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Float:
                    return BitConverter.ToDouble(data, 0).ToString(CultureInfo.InvariantCulture);

                case SqlDbType.Variant:
                    return VariantBinaryToString(data);

                case SqlDbType.Date:
                    return DateTimeConverters.DecodeDate(data).ToShortDateString();

                case SqlDbType.Time:
                    return DateTimeConverters.DecodeTime(data, scale).ToString("HH:mm:ss.fffffff");

                case SqlDbType.DateTime2:
                    return DateTimeConverters.DecodeDateTime2(data, scale).ToString("yyyy-MM-dd HH:mm:ss.fffffff");

                case SqlDbType.DateTimeOffset:
                    return DateTimeConverters.DecodeDateTimeOffset(data, scale);

                default:
                    return string.Format(CultureInfo.CurrentCulture,
                        "not yet supported ({0:G})",
                        sqlType);
            }
        }
        catch
        {
            return "Error converting data";
        }
    }

    public static T? GetValue<T>(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        return (T?)GetValue(data, sqlType, precision, scale);
    }

    public static object? GetValue(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        if (data == null)
        {
            return null;
        }

        try
        {
            return sqlType switch
            {
                SqlDbType.BigInt => BitConverter.ToInt64(data, 0),
                SqlDbType.Int => BitConverter.ToInt32(data, 0),
                SqlDbType.TinyInt => data[0],
                SqlDbType.SmallInt => BitConverter.ToInt16(data, 0),
                SqlDbType.Char => Encoding.UTF8.GetString(data),
                SqlDbType.VarChar => Encoding.UTF8.GetString(data),
                SqlDbType.NChar => Encoding.Unicode.GetString(data),
                SqlDbType.NVarChar => Encoding.Unicode.GetString(data),
                SqlDbType.DateTime => DateTimeConverters.DecodeDateTime(data),
                SqlDbType.SmallDateTime => DateTimeConverters.DecodeSmallDateTime(data),
                SqlDbType.VarBinary => data,
                SqlDbType.Binary => data,
                SqlDbType.UniqueIdentifier => BinaryToGuid(data),
                SqlDbType.Decimal => DecodeDecimal(data, precision, scale),
                SqlDbType.Money => (BitConverter.ToInt64(data, 0) / 10000.0),
                SqlDbType.SmallMoney => (BitConverter.ToInt64(data, 0) / 10000.0),
                SqlDbType.Real => BitConverter.ToSingle(data, 0),
                SqlDbType.Float => BitConverter.ToDouble(data, 0),
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
    /// Returns a string representation of a variant data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    private static string VariantBinaryToString(byte[] data)
    {
        var variantType = data[0];
        var offset = 2;
        byte precision = 0;
        byte scale = 0;

        switch (ToSqlType(variantType))
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
        }

        var variantData = new byte[data.Length - offset];

        Array.Copy(data, offset, variantData, 0, variantData.Length);

        return BinaryToString(variantData, ToSqlType(variantType), precision, scale);
    }

    /// <summary>
    /// Decodes the DECIMAL data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="precision">The precision.</param>
    /// <param name="scale">The scale.</param>
    /// <returns></returns>
    private static SqlDecimal DecodeDecimal(byte[] data, byte precision, byte scale)
    {
        int index;
        var positive = (1 == data[0]);

        var bits = new int[4];

        var length = data.Length >> 2;

        for (index = 0; index < length; index++)
        {
            bits[index] = BitConverter.ToInt32(data, 1 + (index * 4));
        }

        return new SqlDecimal(precision, scale, positive, bits);
    }

    public static List<string> DecodeDataString(string data)
    {
        var decodedData = new List<string>();

        var binaryData = new byte[data.Length / 2];

        for (var i = 0; i < binaryData.Length; i++)
        {
            binaryData[i] = byte.Parse(data.Substring(i * 2, 2),
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
            var bitArray = new BitArray(binaryData);

            var stringBuilder = new StringBuilder();

            for (var i = 0; i < 8; i++)
            {
                stringBuilder.Insert(0, bitArray[i] ? "1" : "0");
            }

            stringBuilder.Insert(0, "binary: ");

            decodedData.Add(stringBuilder.ToString());
        }

        var varcharData = DataString(binaryData, SqlDbType.VarChar);

        if (!string.IsNullOrEmpty(varcharData))
        {
            decodedData.Add(DataString(binaryData, SqlDbType.VarChar));
        }

        return decodedData;
    }

    private static string DataString(byte[] data, SqlDbType sqlType)
    {
        var stringBuilder = new StringBuilder(sqlType.ToString().ToLower(CultureInfo.CurrentCulture));

        stringBuilder.Append(": ");
        try
        {
            stringBuilder.Append(BinaryToString(data, sqlType, 0, 0));
        }
        catch
        {
            stringBuilder.Append("(Error)");
        }

        return stringBuilder.ToString();
    }

    internal static SqlDbType ToSqlType(byte value)
    {
        switch (value)
        {
            case 34:
                return SqlDbType.Image;
            case 35:
                return SqlDbType.Text;
            case 36:
                return SqlDbType.UniqueIdentifier;
            case 48:
                return SqlDbType.TinyInt;
            case 52:
                return SqlDbType.SmallInt;
            case 56:
                return SqlDbType.Int;
            case 58:
                return SqlDbType.SmallDateTime;
            case 59:
                return SqlDbType.Real;
            case 60:
                return SqlDbType.Money;
            case 61:
                return SqlDbType.DateTime;
            case 62:
                return SqlDbType.Float;
            case 98:
                return SqlDbType.Variant;
            case 99:
                return SqlDbType.NText;
            case 104:
                return SqlDbType.Bit;
            case 106:
            case 108:
                return SqlDbType.Decimal;
            case 122:
                return SqlDbType.SmallMoney;
            case 127:
                return SqlDbType.BigInt;
            case 165:
                return SqlDbType.VarBinary;
            case 167:
                return SqlDbType.VarChar;
            case 173:
                return SqlDbType.Binary;
            case 175:
                return SqlDbType.Char;
            case 189:
                return SqlDbType.Timestamp;
            case 231:
                return SqlDbType.NVarChar;
            case 239:
                return SqlDbType.NChar;
            case 241:
                return SqlDbType.Xml;

            //New 2008 Types
            case 40:
                return SqlDbType.Date;
            case 41:
                return SqlDbType.Time;
            case 42:
                return SqlDbType.DateTime2;
            case 43:
                return SqlDbType.DateTimeOffset;

            default:
                return SqlDbType.Variant;
        }
    }

}