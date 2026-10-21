using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class LocatorTable
{
    private LocatorTable(int[] calls, Locator[] locators)
    {
        Calls = calls;
        Locators = locators;
    }

    public int Count => Calls.Length;

    private int[] Calls { get; }

    private Locator[] Locators { get; }

    public static LocatorTable Build(List<(int CallIndex, Locator Locator)> found)
    {
        found.Sort((left, right) => left.CallIndex != right.CallIndex
                                        ? left.CallIndex.CompareTo(right.CallIndex)
                                        : left.Locator.Register.CompareTo(right.Locator.Register));

        return new LocatorTable([.. found.Select(f => f.CallIndex)], [.. found.Select(f => f.Locator)]);
    }

    public IReadOnlyList<Locator> GetLocators(int callIndex)
    {
        var first = SortedSearch.FirstAtOrAfter(Calls, callIndex);

        var end = SortedSearch.FirstAfter(Calls, callIndex, first);

        return end == first ? [] : Locators[first..end];
    }

    public IEnumerable<(int CallIndex, Locator Locator)> Entries()
    {
        for (var index = 0; index < Locators.Length; index++)
        {
            yield return (Calls[index], Locators[index]);
        }
    }
}
