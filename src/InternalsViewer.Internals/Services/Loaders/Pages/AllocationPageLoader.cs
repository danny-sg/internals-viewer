using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services.Loaders.Pages;

public class AllocationPageLoader(IPageLoader pageLoader) : IAllocationPageLoader
{
    public IPageLoader PageLoader { get; } = pageLoader;

    public async Task<AllocationPage> Load(DatabaseDetail databaseDetail, PageAddress pageAddress)
    {
        var page = await PageLoader.Load<AllocationPage>(databaseDetail, pageAddress);

        switch (page.PageHeader.PageType)
        {
            case PageType.Bcm:
            case PageType.Dcm:
            case PageType.Iam:
            case PageType.Sgam:
            case PageType.Gam:
                Load(page);
                break;
            default:
                throw new InvalidOperationException(page.PageHeader.PageType + " is not an allocation page");
        }

        return page;
    }

    /// <summary>
    /// Loads the allocation map.
    /// </summary>
    private void Load(AllocationPage page)
    {
        var allocationData = new byte[AllocationPage.AllocationInterval / 8];

        switch (page.PageHeader.PageType)
        {
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Dcm:
            case PageType.Bcm:
                page.StartPage = new PageAddress(page.PageHeader.PageAddress.FileId, 0);
                break;

            case PageType.Iam:
                page.StartPage = GetIamStartPage(page);
                page.SinglePageSlots = GetSinglePageSlots(page);
                break;
        }

        Array.Copy(page.PageData,
                   AllocationPage.AllocationArrayOffset,
                   allocationData,
                   0,
                   allocationData.Length);

        var bitArray = new BitArray(allocationData);

        bitArray.CopyTo(page.AllocationMap, 0);
    }

    private static List<PageAddress> GetSinglePageSlots(AllocationPage page)
    {
        var singlePageSlots = new List<PageAddress>();

        var offset = AllocationPage.SinglePageSlotOffset;

        for (var i = 0; i < AllocationPage.SlotCount; i++)
        {
            var pageAddress = new byte[PageAddress.Size];

            Array.Copy(page.PageData, offset, pageAddress, 0, PageAddress.Size);

            singlePageSlots.Add(PageAddressParser.Parse(pageAddress));

            offset += PageAddress.Size;
        }

        return singlePageSlots;
    }

    private static PageAddress GetIamStartPage(Page page)
    {
        var pageAddress = new byte[PageAddress.Size];

        Array.Copy(page.PageData, AllocationPage.StartPageOffset, pageAddress, 0, PageAddress.Size);

        return PageAddressParser.Parse(pageAddress);
    }
}