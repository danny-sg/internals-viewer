using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// An IAM allocation structure
/// </summary>
/// <remarks>
/// This is a subclass of Allocation as the BuildChain and Allocated method is overriden with a different method
/// </remarks>
public class IamAllocation : Allocation
{
    public IamAllocation(Database database, PageAddress pageAddress)
        : base(database, pageAddress)
    {
        MultiFile = true;
    }

    public IamAllocation(AllocationPage page)
        : base(page)
    {
        MultiFile = true;
    }

    /// <summary>
    /// Check is a specific extent is allocated
    /// </summary>
    public override bool Allocated(int extent, int fileId)
    {
        var page = Pages.Find(p => p.StartPage.FileId == fileId &&
                                   extent >= (p.StartPage.PageId / 8) &&
                                   extent <= ((p.StartPage.PageId + Database.AllocationInterval) / 8));

        if (page == null)
        {
            return false;
        }

        return page.AllocationMap[extent - (page.StartPage.PageId / 8)];
    }

    /// <summary>
    /// Builds an allocation chain based on linkage through the headers.
    /// </summary>
    protected new void BuildChain(Database database, PageAddress pageAddress)
    {
        var page = new AllocationPage(database, pageAddress);

        Pages.Add(page);
        
        SinglePageSlots.AddRange(page.SinglePageSlots);

        if (page.Header.NextPage != PageAddress.Empty)
        {
            BuildChain(database, page.Header.NextPage);
        }
    }
}