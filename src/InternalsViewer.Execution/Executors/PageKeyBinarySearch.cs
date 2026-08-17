using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces.Pages;

namespace InternalsViewer.Execution.Executors;

/// <summary>
/// Binary search over the slots of a page
/// </summary>
internal static class PageKeyBinarySearch
{
    /// <summary>
    /// Finds the first slot whose key is greater than or equal to the target
    /// </summary>
    /// <remarks>
    /// Returns the slot count when no key qualifies
    /// </remarks>
    public static (int Slot, ImmutableArray<AccessStep.Probe> Probes) LowerBound(IIndexPageAccessor page,
                                                                                 in AccessKey target,
                                                                                 int width)
    {
        return Search(page, target, width, findFirstGreaterThan: false);
    }

    /// <summary>
    /// Finds the first slot whose key is strictly greater than the target
    /// </summary>
    /// <remarks>
    /// Returns the slot count when no key qualifies
    /// </remarks>
    public static (int Slot, ImmutableArray<AccessStep.Probe> Probes) UpperBound(IIndexPageAccessor page,
                                                                                 in AccessKey target,
                                                                                 int width)
    {
        return Search(page, target, width, findFirstGreaterThan: true);
    }

    /// <summary>
    /// Binary Search over page slots to find where a target key belongs
    /// </summary>
    /// <remarks>
    /// Binary search algorithm implementation based on page slots, narrowing to a boundary rather than stopping on a match.
    ///
    /// Values will be in sort order within the page.
    ///
    /// Searches for the target key using the mid point between a low and high value.
    ///
    /// To start, low = Slot 0, high = Page Slot Count.
    ///
    /// Mid Point = low + (high - low) / 2
    ///
    /// Value is compared at the mid point to the key:
    ///
    /// If the value found is larger than the target it means the target must be somewhere between this mid slot and the low slot so high
    /// is moved to the current mid point and the search continues.
    ///
    /// If the value found is smaller than the target it means the target must be somewhere between this mid point and the high slot so
    /// low is moved to the slot after the current mid point and the search continues. The two halves are deliberately not symmetric.
    /// The mid point rounds down, so moving low onto it rather than past it would stop advancing once the window is two slots wide.
    ///
    /// findFirstGreaterThan decides what an equal key at the mid point means. Searching on treats it as too low and settles after the
    /// last of a run of equal keys, which is the upper bound. Stopping treats it as a candidate and settles on the first of that run,
    /// which is the lower bound.
    ///
    /// There is no early exit on an equal key. The search always continues until the window is empty, because leaving early would
    /// settle on an arbitrary member of a run rather than either end of it. What comes back is a position rather than a hit, so it can
    /// be the slot count and it can name a key that does not equal the target at all.
    ///
    /// Search is O(log n) which should be faster than a linear search (reading records in order until match is found) which is O(n).
    /// </remarks>
    private static (int Slot, ImmutableArray<AccessStep.Probe> Probes) Search(IIndexPageAccessor page,
                                                                              in AccessKey target,
                                                                              int width,
                                                                              bool findFirstGreaterThan)
    {
        var probes = ImmutableArray.CreateBuilder<AccessStep.Probe>();

        var low = 0;
        var high = page.SlotCount;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            var comparison = page.CompareKeyPrefix(middle, target, width);

            var searchRight = findFirstGreaterThan ? comparison <= 0 : comparison < 0;

            probes.Add(new AccessStep.Probe(low, high, middle, comparison)
            {
                Key = page.GetKey(middle),
                Target = target,
                Width = width,
                SearchRight = searchRight,
                SlotCount = page.SlotCount
            });

            if (searchRight)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return (low, probes.ToImmutable());
    }
}