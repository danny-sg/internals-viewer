using System.Globalization;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Formatting;

public static partial class Parse
{
    public static PageAddress PageAddress(string text)
    {
        var match = PageAddressPattern().Match(text.Trim());

        if (!match.Success)
        {
            throw new McpException($"'{text}' is not a page address. Use file:page, for example 1:1048.");
        }

        return new PageAddress(short.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                               int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    public static RowIdentifier RowIdentifier(string text)
    {
        var match = RowIdentifierPattern().Match(text.Trim());

        if (!match.Success)
        {
            throw new McpException($"'{text}' is not a row identifier. Use file:page:slot, for example 1:1048:3.");
        }

        return new RowIdentifier(short.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                                 int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                                 ushort.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    public static ulong Hex(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new McpException($"'{text}' is not a hex number.");
    }

    public static ulong Integer(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Hex(trimmed);
        }

        if (ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new McpException($"'{text}' is not a number. Use decimal or 0x hex.");
    }

    public static TracePosition Position(string text)
    {
        var match = PositionPattern().Match(text.Trim());

        if (!match.Success)
        {
            throw new McpException($"'{text}' is not a trace position. Use hex Sequence:Steps, for example 1A3:4F.");
        }

        return new TracePosition(ulong.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                                 ulong.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public static Regex Pattern(string? pattern)
        => new(string.IsNullOrEmpty(pattern) ? string.Empty : pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^(\d+):(\d+)$")]
    private static partial Regex PageAddressPattern();

    [GeneratedRegex(@"^(\d+):(\d+):(\d+)$")]
    private static partial Regex RowIdentifierPattern();

    [GeneratedRegex(@"^([0-9A-Fa-f]+):([0-9A-Fa-f]+)$")]
    private static partial Regex PositionPattern();
}
