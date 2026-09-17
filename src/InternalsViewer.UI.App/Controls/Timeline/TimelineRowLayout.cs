using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Splits the band height across the timeline rows by weight, holding one row at a minimum height when its weighted
/// share would fall short
/// </summary>
/// <remarks>
/// The held row keeps its minimum and the other rows share what is left by their weights, so the heights always sum to
/// the band height. The minimum is only given up when it would leave nothing for the other rows.
/// </remarks>
internal static class TimelineRowLayout
{
    public static float[] Resolve(IReadOnlyList<TimelineRowSet.Row> rows, float height, int heldRow, float heldMinHeight)
    {
        var heights = new float[rows.Count];

        if (rows.Count == 0)
        {
            return heights;
        }

        var totalWeight = rows.Sum(r => r.Weight);

        for (var r = 0; r < rows.Count; r++)
        {
            heights[r] = height * rows[r].Weight / totalWeight;
        }

        if (heldRow < 0 || heldRow >= rows.Count || heights[heldRow] >= heldMinHeight || heldMinHeight >= height)
        {
            return heights;
        }

        heights[heldRow] = heldMinHeight;

        var remainingHeight = height - heldMinHeight;
        var remainingWeight = totalWeight - rows[heldRow].Weight;

        for (var r = 0; r < rows.Count; r++)
        {
            if (r != heldRow)
            {
                heights[r] = remainingWeight > 0 ? remainingHeight * rows[r].Weight / remainingWeight : 0f;
            }
        }

        return heights;
    }
}
