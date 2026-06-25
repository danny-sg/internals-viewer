using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Extensions;

public static class DatabaseSourceExtensions
{
    public static AllocationUnit? FindPageAllocationUnit(this DatabaseSource databaseSource, PageAddress page)
    {
        var extent = page.PageId / 8;

        return databaseSource.AllocationUnits
                             .FirstOrDefault(u =>
                                             u.IamChain.IsExtentAllocated(extent, page.FileId, false) ||
                                             u.IamChain.SinglePageSlots.Contains(page)
                                             || u.FirstPage == page
                                             || u.FirstIamPage == page
                                             || u.RootPage == page);
    }
}