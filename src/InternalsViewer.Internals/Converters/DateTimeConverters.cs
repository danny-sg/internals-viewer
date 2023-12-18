using System;

namespace InternalsViewer.Internals.Converters;

public class DateTimeConverters
{
    /// <summary>
    /// Decodes SMALLDATETIME data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public static DateTime DecodeSmallDateTime(byte[] data)
    {
        var returnDate = new DateTime(1900, 1, 1);

        int timePart = BitConverter.ToUInt16(data, 0);
        int datePart = BitConverter.ToUInt16(data, 2);

        returnDate = returnDate.AddDays(datePart).AddMinutes(timePart);

        return returnDate;
    }

    /// <summary>
    /// Decode DATETIME data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public static DateTime DecodeDateTime(byte[] data)
    {
        var timePart = BitConverter.ToInt32(data, 0);
        var datePart = BitConverter.ToInt32(data, 4);

        return DecodeDateTime(timePart, datePart);
    }

    /// <summary>
    /// Decodes DATETIME data type from 2 integers representing date and time
    /// </summary>
    /// <param name="timePart">The time part.</param>
    /// <param name="datePart">The date part.</param>
    /// <remarks>
    /// SQL Server represents DATETIME as two 4-byte integers.
    /// 
    /// The first integer represents the date part, the number of days since 1st Jan 1900
    /// 
    /// The second integer represents the time part, the number of milliseconds * 3.333 (represented here as 30 / 9)
    /// 
    /// The last oddity is that the time is rounded to the nearest 0, 3 or 7 milliseconds.
    /// </remarks>
    public static DateTime DecodeDateTime(int timePart, int datePart)
    {
        var returnDate = new DateTime(1900, 1, 1);

        var milliseconds = (int)((30f / 9f) * timePart);

        var roundedMilliseconds = milliseconds - milliseconds % 10 + ClosestTo(milliseconds % 10);

        returnDate = returnDate.AddDays(datePart).AddMilliseconds(roundedMilliseconds);

        return returnDate;
    }

    private static int ClosestTo(int number)
    {
        var targets = new[] { 0, 3, 7 };
        var nearest = targets[0];

        var smallestDifference = Math.Abs(number - nearest);

        foreach (var target in targets)
        {
            var difference = Math.Abs(number - target);

            if (difference < smallestDifference)
            {
                nearest = target;
                smallestDifference = difference;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Decodes the DATETIME2 data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    /// <returns></returns>
    public static DateTime DecodeDateTime2(byte[] data, int scale)
    {
        var dateData = new byte[4];
        var timeData = new byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        Array.Copy(data, timeData, data.Length - 3);
        Array.Copy(data, data.Length - 3, dateData, 0, 3);

        var datePart = BitConverter.ToInt32(dateData, 0);
        var timePart = BitConverter.ToInt64(timeData, 0);

        var returnDate = new DateTime(0001, 01, 01);
        returnDate = returnDate.AddDays(datePart);
        returnDate = returnDate.AddMilliseconds(scaleFactor * timePart);

        return returnDate;
    }

    /// <summary>
    /// Decodes the DATE data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public static DateOnly DecodeDate(byte[] data)
    {
        var dateData = new byte[4];

        Array.Copy(data, dateData, 3);

        var date = BitConverter.ToInt32(dateData, 0);

        var returnDate = new DateOnly();

        returnDate = returnDate.AddDays(date);

        return returnDate;
    }

    /// <summary>
    /// Decodes the TIME datatype
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    /// <returns></returns>
    public static TimeSpan DecodeTime(byte[] data, int scale)
    {
        var timeData = new byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        Array.Copy(data, timeData, data.Length);

        var time = BitConverter.ToInt64(timeData, 0);

        var returnDate = new DateTime();
        returnDate = returnDate.AddMilliseconds(scaleFactor * time);

        return returnDate.TimeOfDay;
    }

    /// <summary>
    /// Decodes DATETIMEOFFSET type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>
    /// String representing the value
    /// </returns>
    public static string DecodeDateTimeOffset(byte[] data, byte scale)
    {
        var dateData = new byte[4];
        var timeData = new byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        Array.Copy(data, timeData, data.Length - 5);
        Array.Copy(data, data.Length - 5, dateData, 0, 3);

        var datePart = BitConverter.ToInt32(dateData, 0);
        var timePart = BitConverter.ToInt64(timeData, 0);
        var time = BitConverter.ToInt16(data, data.Length - 2);

        var returnDate = new DateTime(0001, 01, 01);

        returnDate = returnDate.AddDays(datePart);
        returnDate = returnDate.AddMilliseconds(scaleFactor * timePart);

        var offsetTime = new DateTime().AddMinutes(Math.Abs(time));

        string sign;

        if (time >= 0)
        {
            sign = "+";
        }
        else
        {
            sign = "-";
        }

        return $"{returnDate:yyyy-MM-dd HH:mm:ss.fffffff} {sign}{offsetTime:HH:mm}";
    }
}
