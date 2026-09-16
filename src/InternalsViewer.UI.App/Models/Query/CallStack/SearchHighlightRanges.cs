using System;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// Where a search text occurs in a piece of displayed text, for highlighting
/// </summary>
/// <remarks>
/// A <c>module!</c> prefix on the search is not looked for, as it names where to search rather than what. The
/// wildcard-free runs of a pattern are each highlighted. Text that is the search in its entirety gets no ranges,
/// since colouring the whole of an exact match tells the reader nothing.
/// </remarks>
public static class SearchHighlightRanges
{
    private static readonly char[] Wildcards = ['*', '?'];

    public static IReadOnlyList<(int Start, int Length)> Find(string text, string? search)
    {
        var symbol = SymbolPart(search);

        if (text.Length == 0 || symbol.Length == 0 || text.Equals(symbol, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var ranges = new List<(int Start, int Length)>();

        foreach (var segment in symbol.Split(Wildcards, StringSplitOptions.RemoveEmptyEntries))
        {
            var index = 0;

            while ((index = text.IndexOf(segment, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                ranges.Add((index, segment.Length));

                index += segment.Length;
            }
        }

        ranges.Sort();

        return Merge(ranges);
    }

    /// <summary>
    /// The search without any <c>module!</c> prefix, trimmed
    /// </summary>
    public static string SymbolPart(string? search)
    {
        var trimmed = search?.Trim() ?? string.Empty;

        var separator = trimmed.IndexOf('!');

        return separator >= 0 ? trimmed[(separator + 1)..].Trim() : trimmed;
    }

    private static List<(int Start, int Length)> Merge(List<(int Start, int Length)> ranges)
    {
        var merged = new List<(int Start, int Length)>();

        foreach (var range in ranges)
        {
            if (merged.Count > 0 && range.Start <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];

                merged[^1] = (last.Start, Math.Max(last.Start + last.Length, range.Start + range.Length) - last.Start);
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }
}
