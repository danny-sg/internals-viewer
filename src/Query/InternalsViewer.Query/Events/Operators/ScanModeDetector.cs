using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events.Operators;

public sealed record ScanModeResult(ScanMode Mode, string Evidence);

public static class ScanModeDetector
{
    public static ScanModeResult? Detect(PlanNode node, AllocationUnit? allocationUnit, IReadOnlyList<EngineEvent> events)
    {
        if (node.ScanInfo is null || node.PredicateInfo?.HasSeekBounds == true)
        {
            return null;
        }

        if (allocationUnit is { IndexId: 0 })
        {
            return new ScanModeResult(ScanMode.AllocationOrdered,
                                      "A heap has no page chain, so the only scan the storage engine can perform is an allocation order " +
                                      "scan.");
        }

        if (node.ScanInfo.IsOutputOrdered == true)
        {
            return new ScanModeResult(ScanMode.LeafChain,
                                      "The plan requires ordered output, which only the leaf level page chain can provide.");
        }

        if (allocationUnit is null)
        {
            return new ScanModeResult(ScanMode.Indeterminate, "The allocation unit for the operator could not be resolved.");
        }

        var pages = GetReadPages(node, events);

        var iamPages = GetIamPages(allocationUnit);

        foreach (var page in pages)
        {
            if (iamPages.Contains(page))
            {
                return new ScanModeResult(ScanMode.AllocationOrdered,
                                          $"IAM page {page} was read during the operator. The leaf chain never consults the IAM, so the " +
                                          "scan is allocation ordered.");
            }
        }

        foreach (var page in pages)
        {
            if (IsPfsPage(page))
            {
                return new ScanModeResult(ScanMode.AllocationOrdered,
                                          $"PFS page {page} was read during the operator, indicating page allocation status was checked " +
                                          "as an allocation order scan does.");
            }
        }

        return new ScanModeResult(ScanMode.Indeterminate,
                                  "No IAM or PFS page reads are visible for the operator. The allocation pages may already have been " +
                                  "cached, so the scan mode cannot be confirmed from the trace.");
    }

    private static IEnumerable<PageAddress> GetReadPages(PlanNode node, IReadOnlyList<EngineEvent> events)
    {
        return events.OfType<ReadEventGroup>()
                     .Where(e => e.PlanNodeIdentifier?.NodeId == node.NodeId)
                     .SelectMany(e => e.Pages);
    }

    private static HashSet<PageAddress> GetIamPages(AllocationUnit allocationUnit)
    {
        var pages = new HashSet<PageAddress>();

        if (allocationUnit.FirstIamPage != PageAddress.Empty)
        {
            pages.Add(allocationUnit.FirstIamPage);
        }

        foreach (var page in allocationUnit.IamChain.Pages)
        {
            pages.Add(page.PageAddress);
        }

        return pages;
    }

    private static bool IsPfsPage(PageAddress page)
    {
        return page.PageId == 1 || (page.PageId > 0 && page.PageId % PfsPage.PfsInterval == 0);
    }
}
