namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Distributes the Plan row's height across its operators by cost weight while honouring a per-operator
/// minimum, used by the operator-bar renderer
/// </summary>
internal static class OperatorSlotLayout
{
    // Floor under an operator's slot height, in pixels, regardless of its cost share - guarantees every
    // operator stays legible instead of shrinking to nothing next to a much more expensive one. Only
    // given up (see Resolve) when the row is too short for every operator to have it.
    public const float MinOperatorSlotHeight = 10f;

    /// <summary>
    /// Turns cost weights into slot heights that always sum to exactly <paramref name="height"/> (so the
    /// stack never leaves blank space), while keeping every operator at or above
    /// <see cref="MinOperatorSlotHeight"/> - the floor takes priority over exact cost-proportionality,
    /// so an operator forced up to the floor "borrows" height from the others, who then share the
    /// remainder by their original weights. The floor itself is only given up (falling back to a plain
    /// proportional split) when the row is too short for every operator to have it.
    /// </summary>
    public static float[] Resolve(float[] weights, float totalWeight, float height)
    {
        var count = weights.Length;
        var slotHeights = new float[count];

        if (count * MinOperatorSlotHeight > height)
        {
            // Constrained: even everyone's minimum wouldn't fit, so the floor has to give way. Fall back
            // to a plain proportional split, which still always sums to exactly `height`.
            var unit = totalWeight > 0 ? height / totalWeight : height / count;

            for (var i = 0; i < count; i++)
            {
                slotHeights[i] = totalWeight > 0 ? weights[i] * unit : unit;
            }

            return slotHeights;
        }

        // Freeze any operator whose proportional share would fall under the floor at exactly the floor,
        // then re-share the remaining height across the rest by their original weights. Repeat, since
        // shrinking the pool for the remaining operators can push another one under the floor too -
        // this converges because each pass either freezes at least one more operator or stops.
        var frozen = new bool[count];
        var remainingHeight = height;
        var remainingWeight = totalWeight;

        bool anyFrozen;

        do
        {
            anyFrozen = false;

            for (var i = 0; i < count; i++)
            {
                if (frozen[i] || remainingWeight <= 0)
                {
                    continue;
                }

                var share = remainingHeight * weights[i] / remainingWeight;

                if (share < MinOperatorSlotHeight)
                {
                    slotHeights[i] = MinOperatorSlotHeight;
                    frozen[i] = true;
                    remainingHeight -= MinOperatorSlotHeight;
                    remainingWeight -= weights[i];
                    anyFrozen = true;
                }
            }
        } while (anyFrozen);

        for (var i = 0; i < count; i++)
        {
            if (!frozen[i])
            {
                slotHeights[i] = remainingWeight > 0 ? remainingHeight * weights[i] / remainingWeight : 0f;
            }
        }

        return slotHeights;
    }
}
