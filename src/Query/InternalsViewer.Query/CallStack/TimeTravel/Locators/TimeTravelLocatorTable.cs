namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

internal sealed class TimeTravelLocatorTable
{
    private TimeTravelLocatorTable(int[] calls, TimeTravelLocator[] locators)
    {
        Calls = calls;
        Locators = locators;
    }

    public int Count => Calls.Length;

    private int[] Calls { get; }

    private TimeTravelLocator[] Locators { get; }

    public static TimeTravelLocatorTable Build(List<(int Call, TimeTravelLocator Locator)> found)
    {
        found.Sort((left, right) => left.Call != right.Call
                                        ? left.Call.CompareTo(right.Call)
                                        : left.Locator.Register.CompareTo(right.Locator.Register));

        return new TimeTravelLocatorTable([.. found.Select(f => f.Call)], [.. found.Select(f => f.Locator)]);
    }

    public IReadOnlyList<TimeTravelLocator> Of(int call)
    {
        var first = SortedSearch.FirstAtOrAfter(Calls, call);

        var end = SortedSearch.FirstAfter(Calls, call, first);

        return end == first ? [] : Locators[first..end];
    }

    public IEnumerable<(int Call, TimeTravelLocator Locator)> Entries()
    {
        for (var index = 0; index < Locators.Length; index++)
        {
            yield return (Calls[index], Locators[index]);
        }
    }
}
