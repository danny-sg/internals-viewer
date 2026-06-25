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
public sealed class IamPageParser : PageParser, IPageParser<IamPage>
{
    public PageType[] SupportedPageTypes => [PageType.Iam];

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public IamPage Parse(PageData page)
    {
        var iamPage = CopyToPageType<IamPage>(page);

        var allocationUnit = iamPage.Database
                                    .AllocationUnits
                                    .TryGetValue(iamPage.PageHeader.AllocationUnitId, out var value) 
                            ? value 
                            : AllocationUnit.Unknown;

        iamPage.AllocationUnit = allocationUnit;

        var result = Parse(iamPage);

        SetIamMarkers(result);

        return result;
    }

    private static IamPage Parse(IamPage page)
    {
        page.StartPage = GetIamStartPage(page);
        page.SinglePageSlots = GetSinglePageSlots(page);

        var allocationData = page.Data.AsSpan(AllocationPage.AllocationArrayOffset,
                                              AllocationPage.AllocationInterval / 8);

        var allocationMap = page.AllocationMap;

        for (var i = 0; i < allocationData.Length; i++)
        {
            var b = allocationData[i];
            var baseIndex = i * 8;

            allocationMap[baseIndex] = (b & 0x01) != 0;
            allocationMap[baseIndex + 1] = (b & 0x02) != 0;
            allocationMap[baseIndex + 2] = (b & 0x04) != 0;
            allocationMap[baseIndex + 3] = (b & 0x08) != 0;
            allocationMap[baseIndex + 4] = (b & 0x10) != 0;
            allocationMap[baseIndex + 5] = (b & 0x20) != 0;
            allocationMap[baseIndex + 6] = (b & 0x40) != 0;
            allocationMap[baseIndex + 7] = (b & 0x80) != 0;
        }

        return page;
    }

    private static PageAddress[] GetSinglePageSlots(PageData page)
    {
        var singlePageSlots = new PageAddress[AllocationPage.SlotCount];

        var offset = AllocationPage.SinglePageSlotOffset;

        for (var i = 0; i < AllocationPage.SlotCount; i++)
        {
            singlePageSlots[i] = PageAddressParser.Parse(page.Data.AsSpan(offset, PageAddress.Size));

            offset += PageAddress.Size;
        }

        return singlePageSlots;
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
            page.MarkProperty($"SinglePageSlot{i}",
                              AllocationPage.SinglePageSlotOffset + i * PageAddress.Size,
                              PageAddress.Size);
        }

        page.MarkProperty("AllocationMap",
                          AllocationPage.AllocationArrayOffset,
                          AllocationPage.AllocationInterval / 8);
    }
}