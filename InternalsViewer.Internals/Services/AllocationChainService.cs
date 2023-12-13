using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services;

/// <summary>
/// Service responsible for building allocation chains
/// </summary>
/// <remarks>
/// GAM/SGAM/DCM/BCM pages are fixed length bitmaps of extent coverage of around 4GB. Pages are chained based on the GAM interval and file
/// size. The number of pages is derived from the size of the file divided by the GAM interval. 
/// 
/// The GAM interval is sometimes described as 64,000 extents. It is actually 63,904 extents. 
/// 
/// - Page header - 96 bytes
/// - Bitmap size - 7,988 bytes (63,904 bits)
/// - Unused      - 108 bytes
/// </remarks>
public class AllocationChainService(IAllocationPageService pageService) 
    : IAllocationChainService
{
    public IAllocationPageService PageService { get; } = pageService;

    public async Task<AllocationChain> LoadChain(Database database, int fileId, PageType pageType)
    {
        int startPage = pageType switch
        {
            PageType.Gam => 2,
            PageType.Sgam => 3,
            PageType.Dcm => 6,
            PageType.Bcm => 7,
            _ => throw new InvalidOperationException("Page type is not a database allocation page")
        };

        return await LoadChain(database, new PageAddress(fileId, startPage));
    }

    public async Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress)
    {
        var allocation = new AllocationChain();

        var pageCount = (int)Math.Ceiling(database.GetFileSize(startPageAddress.FileId) / (decimal)Database.AllocationInterval);

        for (var i = 1; i < pageCount; i++)
        {
            var address = new PageAddress(startPageAddress.FileId, startPageAddress.PageId + i * Database.AllocationInterval);

            var page = await PageService.Load(database, address);

            allocation.Pages.Add(page);
        }

        return allocation;
    }
}

/// <summary>
/// Service responsible for building IAM chains
/// </summary>
public class IamChainService(IAllocationPageService pageService) : IIamChainService
{
    public IAllocationPageService PageService { get; } = pageService;

    /// <summary>
    /// Loads an IAM chain
    /// </summary>
    /// <remarks>
    /// IAM chains are linked lists of pages linked via the Next Page/Previous Page pointers in the page header
    /// </remarks>
    public async Task<AllocationChain> LoadChain(Database database, PageAddress startPageAddress)
    {
        var iam = new IamChain();

        var pageAddress = startPageAddress; 

        while (true)
        {
            var page = await PageService.Load(database, pageAddress);

            iam.Pages.Add(page);

            iam.SinglePageSlots.AddRange(page.SinglePageSlots);

            if (page.Header.NextPage != PageAddress.Empty)
            {
                pageAddress = page.Header.NextPage;

                continue;
            }

            break;
        }

        return iam;
    }
}