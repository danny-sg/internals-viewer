using System.Globalization;

namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

public readonly record struct ColumnStoreSearch(int? RowGroupId, int? ColumnId)
{
    private static readonly string[] RowGroupNames = ["rowgroup", "rg"];

    private static readonly string[] ColumnNames = ["column", "col"];

    public bool Matches(TimeTravelLocator locator)
        => locator.Kind == LocatorKind.ColumnStoreObject
           && (RowGroupId is not { } rowGroup || locator.RowGroupId == rowGroup)
           && (ColumnId is not { } column || locator.ColumnId == column);

    public static bool TryParse(string? text, out ColumnStoreSearch search)
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

        search = new ColumnStoreSearch(rowGroup, column);

        return true;
    }
}
