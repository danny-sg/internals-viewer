using System.Globalization;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct LocatorSearch(PageAddress Page, ushort? Slot, int? RowGroupId, int? ColumnId)
{
    private const string HexPrefix = "0x";

    private static readonly char[] Separators = [':', ','];

    private static readonly string[] RowGroupNames = ["rowgroup", "rg"];

    private static readonly string[] ColumnNames = ["column", "col"];

    public bool IsColumnStore => RowGroupId is not null || ColumnId is not null;

    public ulong Key => ((ulong)(ushort)Page.FileId << 32) | (uint)Page.PageId | ((ulong)(Slot ?? 0) << 48);

    public static LocatorSearch ForPage(PageAddress page, ushort? slot) => new(page, slot, null, null);

    public static LocatorSearch ForColumnStore(int? rowGroupId, int? columnId) => new(default, null, rowGroupId, columnId);

    public static bool TryParse(string? text, out LocatorSearch search)
        => TryParsePage(text, out search) || TryParseColumnStore(text, out search);

    public bool Matches(Locator locator) => locator.ValueType switch
    {
        LocatorValueType.ColumnStoreObject => IsColumnStore
                                         && (RowGroupId is not { } rowGroup || locator.RowGroupId == rowGroup)
                                         && (ColumnId is not { } column || locator.ColumnId == column),
        LocatorValueType.Page => !IsColumnStore && Slot is null && locator.PageKey == Key,
        LocatorValueType.Row => !IsColumnStore && (Slot is null ? locator.PageKey == Key : locator.Value == Key),
        _ => false
    };

    private static bool TryParsePage(string? text, out LocatorSearch search)
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

        search = ForPage(new PageAddress(file, page), slot);

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

        search = ForPage(row.PageAddress, hex.Length == RowIdentifier.Size * 2 ? row.SlotId : null);

        return true;
    }

    private static bool TryParseColumnStore(string? text, out LocatorSearch search)
    {
        search = default;

        int? rowGroup = null;

        int? column = null;

        foreach (var term in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = term.IndexOf(':');

            if (separator <= 0
                || !int.TryParse(term[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            var name = term[..separator];

            if (RowGroupNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                rowGroup = value;
            }
            else if (ColumnNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                column = value;
            }
            else
            {
                return false;
            }
        }

        if (rowGroup is null && column is null)
        {
            return false;
        }

        search = ForColumnStore(rowGroup, column);

        return true;
    }
}
