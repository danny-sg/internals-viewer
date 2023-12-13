using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
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
        var allocationData = new byte[8000];
        int allocationArrayOffset = AllocationPage.AllocationArrayOffset;

        switch (page.Header.PageType)
        {
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Dcm:
            case PageType.Bcm:

                page.StartPage = new PageAddress(page.Header.PageAddress.FileId, 0);
                break;

            case PageType.Iam:
                page.StartPage = GetIamHeader(page);
                page.SinglePageSlots = GetSinglePageSlots(page);

                break;

            default:
                return;
        }

        Array.Copy(page.PageData,
                   allocationArrayOffset,
                   allocationData,
                   0,
                   allocationData.Length - (page.Header.SlotCount * 2));

        var bitArray = new BitArray(allocationData);

        bitArray.CopyTo(page.AllocationMap, 0);
    }

    private List<PageAddress> GetSinglePageSlots(AllocationPage page)
    {
        var singlePageSlots = new List<PageAddress>();

        var offset = AllocationPage.SinglePageSlotOffset;

        for (var i = 0; i < 8; i++)
        {
            var pageAddress = new byte[6];

            Array.Copy(page.PageData, offset, pageAddress, 0, PageAddress.Size);

            singlePageSlots.Add(new PageAddress(pageAddress));

            offset += 6;
        }

        return singlePageSlots;
    }

    private PageAddress GetIamHeader(AllocationPage page)
    {
        var pageAddress = new byte[6];

        Array.Copy(page.PageData, AllocationPage.StartPageOffset, pageAddress, 0, PageAddress.Size);

        return new PageAddress(pageAddress);
    }
}