using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
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
public class AllocationChainService(IAllocationPageLoader pageLoader) 
    : IAllocationChainService
{
    public IAllocationPageLoader PageLoader { get; } = pageLoader;

    public async Task<AllocationChain> LoadChain(DatabaseDetail databaseDetail, short fileId, PageType pageType)
    {
        var startPage = pageType switch
        {
            PageType.Gam => 2,
            PageType.Sgam => 3,
            PageType.Dcm => 6,
            PageType.Bcm => 7,
            _ => throw new InvalidOperationException("Page type is not a database allocation page")
        };

        return await LoadChain(databaseDetail, new PageAddress(fileId, startPage));
    }

    public async Task<AllocationChain> LoadChain(DatabaseDetail databaseDetail, PageAddress startPageAddress)
    {
        var allocation = new AllocationChain();

        var pageCount = (int)Math.Ceiling(databaseDetail.GetFileSize(startPageAddress.FileId) 
                             / (decimal)AllocationPage.AllocationInterval);

        for (var i = 0; i < pageCount; i++)
        {
            var address = new PageAddress(startPageAddress.FileId, startPageAddress.PageId + i * AllocationPage.AllocationInterval);

            var page = await PageLoader.Load(databaseDetail, address);

            allocation.Pages.Add(page);
        }

        return allocation;
    }
}