using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using System.Threading.Tasks;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Pages.Parsers;

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