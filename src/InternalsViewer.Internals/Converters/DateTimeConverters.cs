using System.Buffers.Binary;

namespace InternalsViewer.Internals.Converters;

public static class DateTimeConverters
{
    /// <summary>
    /// Decodes SMALLDATETIME data type
    /// </summary>
    public static DateTime DecodeSmallDateTime(ReadOnlySpan<byte> data)
    {
        var returnDate = new DateTime(1900, 1, 1);

        int timePart = BinaryPrimitives.ReadUInt16LittleEndian(data);
        int datePart = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);

        return returnDate.AddDays(datePart).AddMinutes(timePart);
    }

    /// <summary>
    /// Decode DATETIME data type
    /// </summary>
    public static DateTime DecodeDateTime(ReadOnlySpan<byte> data)
    {
        var timePart = BinaryPrimitives.ReadInt32LittleEndian(data);
        var datePart = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);

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

        return returnDate.AddDays(datePart).AddMilliseconds(roundedMilliseconds);
    }

    /// <summary>
    /// Decodes the DATETIME2 data type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    public static DateTime DecodeDateTime2(ReadOnlySpan<byte> data, int scale)
    {
        Span<byte> dateData = stackalloc byte[4];
        Span<byte> timeData = stackalloc byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        data[..(data.Length - 3)].CopyTo(timeData);
        data[(data.Length - 3)..].CopyTo(dateData);

        var datePart = BinaryPrimitives.ReadInt32LittleEndian(dateData);
        var timePart = BinaryPrimitives.ReadInt64LittleEndian(timeData);

        return new DateTime(0001, 01, 01)
            .AddDays(datePart)
            .AddMilliseconds(scaleFactor * timePart);
    }

    /// <summary>
    /// Decodes the DATE data type
    /// </summary>
    /// <param name="data">The data.</param>
    public static DateOnly DecodeDate(ReadOnlySpan<byte> data)
    {
        Span<byte> dateData = stackalloc byte[4];

        data[..3].CopyTo(dateData);

        return default(DateOnly).AddDays(BinaryPrimitives.ReadInt32LittleEndian(dateData));
    }

    /// <summary>
    /// Decodes the TIME datatype
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    public static TimeSpan DecodeTime(ReadOnlySpan<byte> data, int scale)
    {
        Span<byte> timeData = stackalloc byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        data.CopyTo(timeData);

        var time = BinaryPrimitives.ReadInt64LittleEndian(timeData);

        return default(DateTime).AddMilliseconds(scaleFactor * time).TimeOfDay;
    }

    /// <summary>
    /// Decodes DATETIMEOFFSET type
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>
    /// String representing the value
    /// </returns>
    public static string DecodeDateTimeOffset(ReadOnlySpan<byte> data, byte scale)
    {
        Span<byte> dateData = stackalloc byte[4];
        Span<byte> timeData = stackalloc byte[8];

        var scaleFactor = 1000F / (float)Math.Pow(10, scale);

        data[..(data.Length - 5)].CopyTo(timeData);
        data[(data.Length - 5)..(data.Length - 2)].CopyTo(dateData);

        var datePart = BinaryPrimitives.ReadInt32LittleEndian(dateData);
        var timePart = BinaryPrimitives.ReadInt64LittleEndian(timeData);
        var time = BinaryPrimitives.ReadInt16LittleEndian(data[(data.Length - 2)..]);

        var returnDate = new DateTime(0001, 01, 01)
            .AddDays(datePart)
            .AddMilliseconds(scaleFactor * timePart);

        var offsetTime = default(DateTime).AddMinutes(Math.Abs(time));
        var sign = time >= 0 ? "+" : "-";

        return $"{returnDate:yyyy-MM-dd HH:mm:ss.fffffff} {sign}{offsetTime:HH:mm}";
    }

    /// <summary>
    /// Finds the closest number to the given number from a list of 0, 3 and 7
    /// </summary>
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
}
