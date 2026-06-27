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

        iamPage.StartPage = GetIamStartPage(page);
        iamPage.SinglePageSlots = GetSinglePageSlots(page);

        page.Data.AsSpan(AllocationPage.AllocationArrayOffset, AllocationPage.AllocationMapBytes)
            .CopyTo(iamPage.AllocationMap);

        SetIamMarkers(iamPage);

        return iamPage;
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
                          AllocationPage.AllocationExtentInterval / 8);
    }
}