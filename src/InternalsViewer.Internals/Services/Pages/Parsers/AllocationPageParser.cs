using System.Collections;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for Allocation pages
/// </summary>
public class AllocationPageParser : PageParser, IPageParser<AllocationPage>
{
    public PageType[] SupportedPageTypes => new[] { PageType.Gam, PageType.Sgam, PageType.Bcm, PageType.Dcm };

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
}