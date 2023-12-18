using System;
using System.Data.SqlTypes;

namespace InternalsViewer.Internals.Converters;

public class DataEncoders
{
    public static string EncodeInt32(int value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeInt16(short value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeInt64(long value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeUInt16(ushort value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeReal(float value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeFloat(double value)
    {
        return BitConverter.ToString(BitConverter.GetBytes(value)).Replace("-", " ");
    }

    public static string EncodeMoney(decimal value)
    {
        return EncodeInt64((long)(value * 10000));
    }

    public static string EncodeSmallMoney(decimal value)
    {
        return EncodeInt32((int)(value * 10000));
    }

    public static string EncodeDecimal(decimal value)
    {
        var sqlValue = new SqlDecimal(value);
        //sqlValue.Precision = precision;
        //sqlValue.Scale = scale;

        return BitConverter.ToString(sqlValue.BinData).Replace("-", " ");
    }

    public static string[] EncodeDateTime(DateTime value)
    {
        var timePart = (int)((value - value.Date).TotalMilliseconds / 3.333333);
        var datePart = (value - new DateTime(1900, 1, 1)).Days;

        return new[] { EncodeInt32(timePart), EncodeInt32(datePart) };
    }

    public static string[] EncodeSmallDateTime(DateTime value)
    {

        var timePart = (ushort)((value - value.Date).TotalMinutes);
        var datePart = (ushort)(value - new DateTime(1900, 1, 1)).Days;

        return new[] { EncodeUInt16(timePart), EncodeUInt16(datePart) };
    }
}