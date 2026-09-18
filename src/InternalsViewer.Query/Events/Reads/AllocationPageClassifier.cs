using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Reads;

public static class AllocationPageClassifier
{
    public static void Classify(IReadOnlyList<EngineEvent> events)
    {
        var iamPages = IamPages(events);

        foreach (var engineEvent in events)
        {
            if (engineEvent is ReadEventGroup read)
            {
                read.IsAllocationPage = read.Pages.Any(p => iamPages.Contains(p) || PageNameHelper.TryGetPageName(p) is not null);
            }
        }
    }

    private static HashSet<PageAddress> IamPages(IReadOnlyList<EngineEvent> events)
    {
        var pages = new HashSet<PageAddress>();

        var units = new HashSet<long>();

        foreach (var engineEvent in events)
        {
            if (engineEvent.AllocationUnit is not { } unit || !units.Add(unit.AllocationUnitId))
            {
                continue;
            }

            foreach (var iam in unit.IamChain.Pages)
            {
                pages.Add(iam.PageAddress);
            }
        }

        return pages;
    }
}
