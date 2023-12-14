using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders;

public class AllocationPageService(IPageService pageService): IAllocationPageService
{
    public IPageService PageService { get; } = pageService;

    public async Task<AllocationPage> Load(Database database, PageAddress pageAddress)
    {
        var page = await PageService.Load<AllocationPage>(database, pageAddress);

        switch (page.Header.PageType)
        {
            case PageType.Bcm:
            case PageType.Dcm:
            case PageType.Iam:
            case PageType.Sgam:
            case PageType.Gam:
                LoadAllocationMap(page);
                break;
            default:
                throw new InvalidOperationException(page.Header.PageType + " is not an allocation page");
        }

        return page;
    }

    /// <summary>
    /// Loads the allocation map.
    /// </summary>
    private void LoadAllocationMap(AllocationPage page)
    {
        var allocationData = new byte[AllocationPage.AllocationInterval / 8];

        switch (page.Header.PageType)
        {
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Dcm:
            case PageType.Bcm:
                page.StartPage = new PageAddress(page.Header.PageAddress.FileId, 0);
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

    private static PageAddress GetIamStartPage(AllocationPage page)
    {
        var pageAddress = new byte[PageAddress.Size];

        Array.Copy(page.PageData, AllocationPage.StartPageOffset, pageAddress, 0, PageAddress.Size);

        return PageAddressParser.Parse(pageAddress);
    }
}