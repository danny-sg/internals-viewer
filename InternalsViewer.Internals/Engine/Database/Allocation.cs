using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// An Allocation structure represented by a collection of allocation pages separated by an interval
/// </summary>
public class Allocation
{
    private int interval;

    public Allocation(Database database, PageAddress pageAddress)
    {
        FileId = pageAddress.FileId;
        IsMultiFile = false;

        BuildChain(database, pageAddress);
    }

    public Allocation(AllocationPage page)
    {
        FileId = page.PageAddress.FileId;
        IsMultiFile = false;
        Pages.Add(page);
        interval = Database.AllocationInterval;

        SinglePageSlots.AddRange(page.SinglePageSlots);
    }

    /// <summary>
    /// Check if a specific extent is allocated
    /// </summary>
    public virtual bool IsAllocated(int extent, int allocationFileId)
    {
        return Pages[(extent * 8) / interval].AllocationMap[extent % ((interval / 8) + 1)];
    }

    /// <summary>
    /// Checks the allocation status or an extent
    /// </summary>
    public static bool CheckAllocationStatus(int targetExtent, int fileId, bool invert, Allocation chain)
    {
        return (!invert
                && chain.IsAllocated(targetExtent, fileId)
                && (fileId == chain.FileId || chain.IsMultiFile)
               )
               ||
               (invert
                && !chain.IsAllocated(targetExtent, fileId)
                && (fileId == chain.FileId || chain.IsMultiFile)
               );
    }

    /// <summary>
    /// Builds the allocation chain following an interval
    /// </summary>
    protected virtual void BuildChain(Database database, PageAddress pageAddress)
    {
        var page = new AllocationPage(database, pageAddress);

        if (page.Header.PageType == PageType.Iam)
        {
            //throw new ArgumentException();
        }

        Pages.Add(page);

        SinglePageSlots.AddRange(page.SinglePageSlots);

        interval = Database.AllocationInterval;

        var pageCount = (int)Math.Ceiling(database.FileSize(pageAddress.FileId) / (decimal)interval);

        if (pageCount > 1)
        {
            for (var i = 1; i < pageCount; i++)
            {
                Pages.Add(new AllocationPage(database,
                                             new PageAddress(pageAddress.FileId, pageAddress.PageId + i * interval)));
            }
        }
    }

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    public void Refresh()
    {
        SinglePageSlots.Clear();

        foreach (var page in Pages)
        {
            page.Refresh();
            SinglePageSlots.AddRange(page.SinglePageSlots);
        }
    }

    /// <summary>
    /// Returns a page address for the allocation page that corresponds to a page address and allocation page type
    /// </summary>
    public static PageAddress AllocationPageAddress(PageAddress pageAddress, PageType pageType)
    {
        int pageId;

        switch (pageType)
        {
            case PageType.Pfs:

                if (pageAddress.PageId < Database.PfsInterval)
                {
                    return new PageAddress(pageAddress.FileId, 1);
                }

                pageId = (pageAddress.PageId / Database.PfsInterval) * Database.PfsInterval;

                return new PageAddress(pageAddress.FileId, pageId);

            default:
                if (pageAddress.PageId < Database.AllocationInterval)
                {
                    pageId = 2;

                    switch (pageType)
                    {
                        case PageType.Sgam:

                            pageId += 1;
                            break;

                        case PageType.Dcm:

                            pageId += 4;
                            break;

                        case PageType.Bcm:

                            pageId += 5;
                            break;
                    }
                }
                else
                {
                    pageId = (pageAddress.PageId / Database.AllocationInterval) * Database.AllocationInterval;

                    switch (pageType)
                    {
                        case PageType.Sgam:

                            pageId += 1;
                            break;

                        case PageType.Dcm:

                            pageId += 6;
                            break;

                        case PageType.Bcm:

                            pageId += 7;
                            break;
                    }
                }

                return new PageAddress(pageAddress.FileId, pageId);
        }
    }

    /// <summary>
    /// Gets the pages.
    /// </summary>
    /// <value>The pages.</value>
    public List<AllocationPage> Pages { get; } = new();

    /// <summary>
    /// Gets or sets the single page slots.
    /// </summary>
    public List<PageAddress> SinglePageSlots { get; set; } = new();

    /// <summary>
    /// Gets or sets the file id.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// Determines if the Allocation spans multiple files
    /// </summary>
    public bool IsMultiFile { get; set; }
}