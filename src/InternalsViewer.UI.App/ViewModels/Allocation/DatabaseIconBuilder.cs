using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

/// <summary>
/// Builds a quantized version of the app logo coloured by a database's allocations
/// </summary>
/// <remarks>
/// Object layers are apportioned across the nine cells by page count and filled left to right then top to bottom,
/// largest first. The trailing cells are reserved for layers other than the largest, so a single dominant object
/// cannot take the whole icon. System objects and the allocation overlay layers take no part.
/// </remarks>
internal static class DatabaseIconBuilder
{
    public const int ColumnCount = 3;

    public const int RowCount = 3;

    public const int CellCount = ColumnCount * RowCount;

    private const int ReservedCellCount = 2;

    public static IReadOnlyList<Color> DefaultCells { get; } =
    [
        Color.FromArgb(0x78, 0xFA, 0x9A), Color.FromArgb(0x5C, 0xDE, 0x96), Color.FromArgb(0x60, 0xE2, 0x9A),
        Color.FromArgb(0xA0, 0xFA, 0x78), Color.FromArgb(0x5C, 0xDC, 0xDE), Color.FromArgb(0x60, 0xE0, 0xE2),
        Color.FromArgb(0x78, 0xC7, 0xFA), Color.FromArgb(0x5C, 0xAB, 0xDE), Color.FromArgb(0x60, 0xAF, 0xE2)
    ];

    public static IReadOnlyList<Color> Build(IEnumerable<AllocationLayer> layers)
    {
        var ordered = layers.Where(l => !l.IsAllocationLayer && !l.IsSystemObject && l.TotalPages > 0)
                            .OrderByDescending(l => l.TotalPages)
                            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                            .ToList();

        if (ordered.Count == 0)
        {
            return DefaultCells;
        }

        var counts = Apportion([.. ordered.Select(l => l.TotalPages)]);

        ReserveMinorityCells(counts);

        var cells = new Color[CellCount];

        var cellIndex = 0;

        for (var index = 0; index < ordered.Count && cellIndex < CellCount; index++)
        {
            for (var i = 0; i < counts[index] && cellIndex < CellCount; i++)
            {
                cells[cellIndex] = ordered[index].Colour;

                cellIndex++;
            }
        }

        return cells;
    }

    private static int[] Apportion(IReadOnlyList<long> pageCounts)
    {
        var total = pageCounts.Sum();

        var counts = new int[pageCounts.Count];

        var assigned = 0;

        for (var index = 0; index < pageCounts.Count; index++)
        {
            counts[index] = (int) (pageCounts[index] * CellCount / total);

            assigned += counts[index];
        }

        var byRemainder = Enumerable.Range(0, pageCounts.Count)
                                    .OrderByDescending(i => pageCounts[i] * CellCount % total)
                                    .ToList();

        for (var index = 0; assigned < CellCount; index++, assigned++)
        {
            counts[byRemainder[index % byRemainder.Count]]++;
        }

        return counts;
    }

    private static void ReserveMinorityCells(int[] counts)
    {
        if (counts.Length < 2)
        {
            return;
        }

        var cap = CellCount - Math.Min(ReservedCellCount, counts.Length - 1);

        var surplus = counts[0] - cap;

        if (surplus <= 0)
        {
            return;
        }

        counts[0] = cap;

        var index = 1;

        while (surplus > 0)
        {
            counts[index]++;

            surplus--;

            index = index + 1 == counts.Length ? 1 : index + 1;
        }
    }
}
