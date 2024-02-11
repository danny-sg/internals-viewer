using System.Collections;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for IAM (Index Allocation Map) pages
/// </summary>
public class IamPageParser : PageParser, IPageParser<IamPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.Iam };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public IamPage Parse(PageData page)
    {
        var iamPage = CopyToPageType<IamPage>(page);

        iamPage.AllocationUnit = iamPage.Database
                                        .AllocationUnits
                                        .FirstOrDefault(a => a.AllocationUnitId == iamPage.PageHeader.AllocationUnitId)
                                 ?? AllocationUnit.Unknown;

        var result =  Parse(iamPage);

        SetIamMarkers(result);

        return result;
    }

    private static IamPage Parse(IamPage page)
    {
        page.StartPage = GetIamStartPage(page);
        page.SinglePageSlots = GetSinglePageSlots(page);

        var allocationData = new byte[AllocationPage.AllocationInterval / 8];

        Array.Copy(page.Data,
                   AllocationPage.AllocationArrayOffset,
                   allocationData,
                   0,
                   allocationData.Length);

        var bitArray = new BitArray(allocationData);

        bitArray.CopyTo(page.AllocationMap, 0);

        return page;
    }

    private static PageAddress[] GetSinglePageSlots(PageData page)
    {
        var singlePageSlots = new List<PageAddress>();

        var offset = AllocationPage.SinglePageSlotOffset;

        for (var i = 0; i < AllocationPage.SlotCount; i++)
        {
            var pageAddress = new byte[PageAddress.Size];

            Array.Copy(page.Data, offset, pageAddress, 0, PageAddress.Size);

            singlePageSlots.Add(PageAddressParser.Parse(pageAddress));

            offset += PageAddress.Size;
        }

        return singlePageSlots.ToArray();
    }

    private static PageAddress GetIamStartPage(PageData page)
    {
        return PageAddressParser.Parse(page.Data, IamPage.StartPageOffset);
    }

    private static void SetIamMarkers(DataStructure page)
    {
        page.MarkProperty("StartPage", IamPage.StartPageOffset, PageAddress.Size);

        for (var i = 0; i < 8; i++)
        {
            page.MarkProperty($"SinglePageSlot{i}", AllocationPage.SinglePageSlotOffset + i * PageAddress.Size, PageAddress.Size);
        }

        page.MarkProperty("AllocationMap", AllocationPage.AllocationArrayOffset, AllocationPage.AllocationInterval / 8);
    }
}