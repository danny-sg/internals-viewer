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
        var allocationPage = CopyToPageType<AllocationPage>(page);

        page.Data.AsSpan(AllocationPage.AllocationArrayOffset, AllocationPage.AllocationMapBytes)
            .CopyTo(allocationPage.AllocationMap);

        return allocationPage;
    }
}