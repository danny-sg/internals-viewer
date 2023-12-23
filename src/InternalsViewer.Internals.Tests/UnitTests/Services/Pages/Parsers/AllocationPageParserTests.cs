using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

public class AllocationPageParserTests(ITestOutputHelper testOutput)
    : PageParserTestsBase(testOutput)
{
    [Theory]
    [InlineData(AllocationPage.FirstGamPage, PageType.Gam)]
    [InlineData(AllocationPage.FirstSgamPage, PageType.Sgam)]
    [InlineData(AllocationPage.FirstDcmPage, PageType.Dcm)]
    [InlineData(AllocationPage.FirstBcmPage, PageType.Bcm)]
    public async Task Can_Parse_Allocation_Page(int pageId, PageType pageType)
    {
        var pageData = await GetPageData(new PageAddress(1, pageId));

        var parser = new AllocationPageParser();

        var allocationPage = parser.Parse(pageData);

        Assert.True(allocationPage.PageHeader.PageType == pageType);
    }
}