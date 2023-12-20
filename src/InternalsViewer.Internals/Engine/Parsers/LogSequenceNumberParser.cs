using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Parsers;

public static class LogSequenceNumberParser
{
    public static LogSequenceNumber Parse(string value)
    {
        // Check if in hex format with regex
        var matchHex = Regex.Match(value, @"([0-9A-Fa-f]{8}):([0-9A-Fa-f]{8}):([0-9A-Fa-f]{4})");

        var matchNumber = Regex.Match(value, @"(\d+):(\d+):(\d+)");

        if (matchHex.Success)
        {
            return new LogSequenceNumber(int.Parse(matchHex.Groups[1].Value, System.Globalization.NumberStyles.HexNumber),
                                         int.Parse(matchHex.Groups[2].Value, System.Globalization.NumberStyles.HexNumber),
                                         short.Parse(matchHex.Groups[3].Value, System.Globalization.NumberStyles.HexNumber));
        }

        if (matchNumber.Success)
        {
            return new LogSequenceNumber(int.Parse(matchNumber.Groups[1].Value),
                                         int.Parse(matchNumber.Groups[2].Value),
                                         short.Parse(matchNumber.Groups[3].Value));
        }

        throw new ArgumentException("Invalid Log Sequence Number format", nameof(value));
    }

    public static LogSequenceNumber Parse(byte[] data, int startIndex)
    {
        var virtualLogFile = BitConverter.ToInt32(data, startIndex);
        var fileOffset = BitConverter.ToInt32(data, startIndex + 4);
        var recordSequence = BitConverter.ToInt16(data, startIndex + 8);

        return new LogSequenceNumber(virtualLogFile, fileOffset, recordSequence);
    }

    public static LogSequenceNumber Parse(byte[] value)
    {
        return Parse(value, 0);
    }
}