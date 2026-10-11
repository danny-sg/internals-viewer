using System.Globalization;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

public readonly record struct LocatorSearch(PageAddress Page, ushort? Slot)
{
    private const string HexPrefix = "0x";

    private static readonly char[] Separators = [':', ','];

    public ulong Key => ((ulong)(ushort)Page.FileId << 32) | (uint)Page.PageId | ((ulong)(Slot ?? 0) << 48);

    public static bool TryParse(string? text, out LocatorSearch search)
    {
        search = default;

        var trimmed = text?.Trim().Trim('(', ')').Trim() ?? string.Empty;

        if (trimmed.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseBytes(trimmed[HexPrefix.Length..], out search);
        }

        var parts = trimmed.Split(Separators, StringSplitOptions.TrimEntries);

        if (parts.Length is < 2 or > 3
            || !short.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var file)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var page))
        {
            return false;
        }

        ushort? slot = null;

        if (parts.Length == 3)
        {
            if (!ushort.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            slot = parsed;
        }

        search = new LocatorSearch(new PageAddress(file, page), slot);

        return true;
    }

    private static bool TryParseBytes(string hex, out LocatorSearch search)
    {
        search = default;

        if (hex.Length is not (PageAddress.Size * 2 or RowIdentifier.Size * 2)
            || hex.Any(c => !char.IsAsciiHexDigit(c)))
        {
            return false;
        }

        var row = new RowIdentifier(Convert.FromHexString(hex));

        search = new LocatorSearch(row.PageAddress, hex.Length == RowIdentifier.Size * 2 ? row.SlotId : null);

        return true;
    }
}
