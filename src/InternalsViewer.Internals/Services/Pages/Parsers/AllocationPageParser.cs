using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Allocation pages
/// </summary>
public sealed class AllocationPageParser : PageParser, IPageParser<AllocationPage>
{
    public PageType[] SupportedPageTypes => [PageType.Gam, PageType.Sgam, PageType.Bcm, PageType.Dcm];

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public AllocationPage Parse(PageData page)
    {
        var allocationPage = CopyToPageType<AllocationPage>(page);

        return Parse(allocationPage);
    }

    private static AllocationPage Parse(AllocationPage page)
    {
        var allocationData = page.Data.AsSpan(AllocationPage.AllocationArrayOffset,
                                              AllocationPage.AllocationInterval / 8);

        var allocationMap = page.AllocationMap;

        for (var i = 0; i < allocationData.Length; i++)
        {
            var b = allocationData[i];

            var baseIndex = i * 8;

            // Set bits from the 8 bits in the byte
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
}