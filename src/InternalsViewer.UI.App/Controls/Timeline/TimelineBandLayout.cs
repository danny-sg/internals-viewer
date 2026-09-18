using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Splits the band height across the timeline rows by weight, holding one row at a minimum height when its weighted
/// share would fall short
/// </summary>
/// <remarks>
/// The held row keeps its minimum and the other rows share what is left by their weights, so the heights always sum to
/// the band height. The minimum is only given up when it would leave nothing for the other rows.
/// </remarks>
internal static class TimelineBandLayout
{
    public static float[] Resolve(IReadOnlyList<TimelineBand> bands, float height, int heldBand, float heldMinHeight)
    {
        var heights = new float[bands.Count];

        if (bands.Count == 0)
        {
            return heights;
        }

        var totalWeight = bands.Sum(r => r.Weight);

        for (var r = 0; r < bands.Count; r++)
        {
            heights[r] = height * bands[r].Weight / totalWeight;
        }

        if (heldBand < 0 || heldBand >= bands.Count || heights[heldBand] >= heldMinHeight || heldMinHeight >= height)
        {
            return heights;
        }

        heights[heldBand] = heldMinHeight;

        var remainingHeight = height - heldMinHeight;
        var remainingWeight = totalWeight - bands[heldBand].Weight;

        for (var r = 0; r < bands.Count; r++)
        {
            if (r != heldBand)
            {
                heights[r] = remainingWeight > 0 ? remainingHeight * bands[r].Weight / remainingWeight : 0f;
            }
        }

        return heights;
    }
}
