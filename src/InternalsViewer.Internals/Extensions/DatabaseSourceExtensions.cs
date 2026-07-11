using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Extensions;

public static class DatabaseSourceExtensions
{
    public static AllocationUnit? FindPageAllocationUnit(this DatabaseSource databaseSource, PageAddress page)
    {
        // A page can sit in an extent uniformly allocated to an object yet not itself be allocated (a free page in the
        // object's extent). The PFS is authoritative per page, so if it marks the page unallocated it belongs to no
        // object — return null rather than attributing it to the extent's owner. Only applied when the PFS is loaded.
        if (databaseSource.Pfs.TryGetValue(page.FileId, out var pfs) && !pfs.GetPageStatus(page.PageId).IsAllocated)
        {
            return null;
        }

        var extent = page.PageId / 8;

        return databaseSource.AllocationUnits
                             .Values
                             .FirstOrDefault(u =>
                                             u.IamChain.IsExtentAllocated(extent, page.FileId, false) ||
                                             u.IamChain.SinglePageSlots.Contains(page)
                                             || (u.FirstPage == page && u.IndexType == IndexType.Clustered)
                                             || u.FirstIamPage == page
                                             || (u.RootPage == page && u.IndexType == IndexType.Clustered));
    }
}