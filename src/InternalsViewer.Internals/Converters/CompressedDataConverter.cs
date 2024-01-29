using System.Data;
using System.Globalization;

namespace InternalsViewer.Internals.Converters;

public class CompressedDataConverter
{
    public static string CompressedBinaryToBinary(byte[]? data, SqlDbType sqlType, byte precision, byte scale)
    {
        if (data == null || data.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var unsigned = (data[0] & 0x80) == 0x80;

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
                case SqlDbType.SmallMoney:
                    return DataConverter.BinaryToString(DecodeInt(data, unsigned, 8), sqlType, precision, scale);

                case SqlDbType.NChar:
                case SqlDbType.NVarChar:
                    return DataConverter.BinaryToString(data, SqlDbType.VarChar, precision, scale);

                case SqlDbType.DateTime:
                    return DecodeDateTime(data, unsigned);

                case SqlDbType.SmallDateTime:
                    return DecodeSmallDateTime(data);

                //case SqlDbType.VarBinary:
                //case SqlDbType.Binary:
                //    break;

                //case SqlDbType.UniqueIdentifier:
                //    break;

                //case SqlDbType.Decimal:
                //    break;

                //case SqlDbType.Money:
                //case SqlDbType.SmallMoney:

                //    break;

                //case SqlDbType.Real:
                //    break;

                //case SqlDbType.Float:
                //    break;

                //case SqlDbType.Variant:
                //    break;

                ////New 2008 types (STC)
                //case SqlDbType.Date:
                //    break;

                //case SqlDbType.Time:

                //    break;

                //case SqlDbType.DateTime2:

                //    break;

                //case SqlDbType.DateTimeOffset:

                //    break;

                case SqlDbType.Binary:
                case SqlDbType.Bit:
                case SqlDbType.Char:
                case SqlDbType.Decimal:
                case SqlDbType.Float:
                case SqlDbType.Image:
                case SqlDbType.NText:
                case SqlDbType.Real:
                case SqlDbType.UniqueIdentifier:
                case SqlDbType.Text:
                case SqlDbType.Timestamp:
                case SqlDbType.VarBinary:
                case SqlDbType.VarChar:
                case SqlDbType.Variant:
                case SqlDbType.Xml:
                case SqlDbType.Udt:
                case SqlDbType.Structured:
                case SqlDbType.Date:
                case SqlDbType.Time:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                default:
                    return DataConverter.BinaryToString(data, sqlType, precision, scale);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DecodeBigInt(byte[] data, bool unsigned)
    {

        var returnData = DecodeInt(data, unsigned, 8);

        if (unsigned)
        {
            return BitConverter.ToUInt64(returnData, 0).ToString(CultureInfo.CurrentCulture);
        }

        return BitConverter.ToInt64(returnData, 0).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeSmallInt(byte[] data, bool unsigned)
    {
        var returnData = DecodeInt(data, unsigned, 2);

        if (unsigned)
        {
            return BitConverter.ToUInt16(returnData, 0).ToString(CultureInfo.CurrentCulture);
        }

        return BitConverter.ToInt16(returnData, 0).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeInt(byte[] data, bool unsigned)
    {
        var returnData = DecodeInt(data, unsigned, 4);

        if (unsigned)
        {
            return BitConverter.ToUInt32(returnData, 0).ToString(CultureInfo.CurrentCulture);
        }

        return BitConverter.ToInt32(returnData, 0).ToString(CultureInfo.CurrentCulture);
    }

    private static string DecodeSmallDateTime(byte[] data)
    {
        if (data.Length == 2)
        {
            var expandedDate = new byte[4];

            Array.Copy(data, 0, expandedDate, 2, 2);

            return DataConverter.BinaryToString(expandedDate, SqlDbType.SmallDateTime);
        }

        return DataConverter.BinaryToString(data, SqlDbType.SmallDateTime);
    }

    private static string DecodeDateTime(byte[] data, bool unsigned)
    {
        var expandedDateTime = new byte[8];

        if (data.Length < 5)
        {
            //Time only
            Array.Copy(DecodeInt(data, unsigned, 4), expandedDateTime, data.Length);
        }
        else
        {
            //time is always the last 4 bytes

            var timePart = new byte[4];
            var datePart = new byte[data.Length - 4];

            Array.Copy(data, data.Length - 4, timePart, 0, 4);
            Array.Copy(data, datePart, data.Length - 4);

            Array.Copy(DecodeInt(timePart, (timePart[0] & 0x80) == 0x80, 4), expandedDateTime, timePart.Length);

            if (!unsigned)
            {
                datePart[0] = Convert.ToByte(datePart[0] ^ 0x80);
            }

            Array.Copy(DecodeInt(datePart, unsigned, 4), 0, expandedDateTime, 4, 4);
        }

        return DataConverter.BinaryToString(expandedDateTime, SqlDbType.DateTime);
    }

    private static byte[] DecodeInt(byte[] data, bool unsigned, int size)
    {
        var returnData = new byte[size];

        if (!unsigned)
        {
            for (var i = 0; i < returnData.Length; i++)
            {
                returnData[i] = 0xFF;
            }
        }
        else
        {
            data[0] = Convert.ToByte(data[0] ^ 0x80);
        }

        Array.Reverse(data);

        Array.Copy(data, returnData, data.Length);

        return returnData;
    }

    public static int DecodeInternalInt(byte[] data, int startPos)
    {
        if ((data[startPos] & 0x80) != 0 && data.Length > 1)
        {
            var numberOfColumnsData = new byte[2];

            Array.Copy(data, startPos, numberOfColumnsData, 0, 2);

            numberOfColumnsData[0] = Convert.ToByte(numberOfColumnsData[0] ^ 0x80);

            Array.Reverse(numberOfColumnsData);

            return BitConverter.ToUInt16(numberOfColumnsData, 0);
        }

        return data[startPos];
    }
}