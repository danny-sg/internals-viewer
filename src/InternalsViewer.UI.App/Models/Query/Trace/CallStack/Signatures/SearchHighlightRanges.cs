using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;

/// <summary>
/// Where a search text occurs in a piece of displayed text, for highlighting
/// </summary>
/// <remarks>
/// A search with <c>|</c> in it is several, and each is highlighted. A <c>module!</c> prefix is not looked for, as it names where to search
/// rather than what.
///
/// The wildcard-free runs of a pattern are each highlighted. Exact matches do not get highlighted.
/// </remarks>
public static class SearchHighlightRanges
{
    private static readonly char[] Wildcards = ['*', '?'];

    public static IReadOnlyList<(int Start, int Length)> Find(string text, string? search)
    {
        var symbols = Alternatives(search).Select(SymbolPart).Where(s => s.Length > 0).ToList();

        if (text.Length == 0 || symbols.Any(s => text.Equals(s, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var ranges = new List<(int Start, int Length)>();

        foreach (var segment in symbols.SelectMany(s => s.Split(Wildcards, StringSplitOptions.RemoveEmptyEntries)))
        {
            var index = 0;

            while ((index = text.IndexOf(segment, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                ranges.Add((index, segment.Length));

                index += segment.Length;
            }
        }

        return [.. Intervals.Coalesce(ranges, r => r.Start, r => r.Start + r.Length).Select(r => (r.Start, Length: r.End - r.Start))];
    }

    /// <summary>
    /// The searches a text holds, split on <c>|</c>
    /// </summary>
    public static IReadOnlyList<string> Alternatives(string? search) =>
        (search ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The search without any <c>module!</c> prefix, trimmed
    /// </summary>
    public static string SymbolPart(string? search)
    {
        var trimmed = search?.Trim() ?? string.Empty;

        var separator = trimmed.IndexOf('!');

        return separator >= 0 ? trimmed[(separator + 1)..].Trim() : trimmed;
    }
}
