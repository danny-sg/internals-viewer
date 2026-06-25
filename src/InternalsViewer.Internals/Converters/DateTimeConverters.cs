using System.Buffers.Binary;

namespace InternalsViewer.Internals.Converters;

public static class DateTimeConverters
{
    private static readonly long[] TickFactors = [10_000_000L, 1_000_000L, 100_000L, 10_000L, 1_000L, 100L, 10L, 1L];

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

        data[..^3].CopyTo(timeData);
        data[^3..].CopyTo(dateData);

        var datePart = BinaryPrimitives.ReadInt32LittleEndian(dateData);
        var timePart = BinaryPrimitives.ReadInt64LittleEndian(timeData);

        return new DateTime(0001, 01, 01)
            .AddDays(datePart)
            .AddTicks(timePart * TickFactors[scale]);
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
    /// <remarks>
    /// TIME is stored as a 3, 4, 5, 6, 7 or 8 byte integer depending on the scale. The integer represents the number
    /// of ticks since midnight.
    ///
    ///     Scale | Unit   | Storage
    ///     ------+--------+--------
    ///     0     |    1 s |  3 bytes
    ///     1     | 100 ms |  3 bytes
    ///     2     |  10 ms |  3 bytes
    ///     3     |   1 ms |  4 bytes
    ///     4     | 100 us |  4 bytes
    ///     5     |  10 us |  5 bytes
    ///     6     |   1 us |  5 bytes
    ///     7     | 100 ns |  5 bytes (.NET tick)
    /// </remarks>
    /// <param name="data">The data.</param>
    /// <param name="scale">The scale.</param>
    public static TimeSpan DecodeTime(ReadOnlySpan<byte> data, int scale)
    {
        Span<byte> timeData = stackalloc byte[8];

        data.CopyTo(timeData);

        var time = BinaryPrimitives.ReadInt64LittleEndian(timeData);

        return TimeSpan.FromTicks(time * TickFactors[scale]);
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

        data[..^5].CopyTo(timeData);
        data[^5..^2].CopyTo(dateData);

        var datePart = BinaryPrimitives.ReadInt32LittleEndian(dateData);
        var timePart = BinaryPrimitives.ReadInt64LittleEndian(timeData);
        var time = BinaryPrimitives.ReadInt16LittleEndian(data[^2..]);

        var returnDate = new DateTime(0001, 01, 01)
            .AddDays(datePart)
            .AddTicks(timePart * TickFactors[scale]);

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
