using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

/// <summary>
/// Binary search over the slots of a page
/// </summary>
public static class AccessPathSearch
{
    /// <summary>
    /// Finds the first slot whose key is greater than or equal to the target
    /// </summary>
    /// <remarks>
    /// Returns the slot count when no key qualifies.
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
    public static (int Slot, ImmutableArray<AccessStep.Probe> Probes) UpperBound(IIndexPageAccessor page,
                                                                                 in AccessKey target,
                                                                                 int width)
    {
        return Search(page, target, width, findFirstGreaterThan: true);
    }

    /// <summary>
    /// Binary Search over page slots
    /// </summary>
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
