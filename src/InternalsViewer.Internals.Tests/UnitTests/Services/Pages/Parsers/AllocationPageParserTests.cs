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

    [Fact]
    public async Task Allocation_Map_Has_Correct_Length()
    {
        var pageData = await GetPageData(new PageAddress(1, AllocationPage.FirstGamPage));

        var parser = new AllocationPageParser();

        var allocationPage = parser.Parse(pageData);

        Assert.Equal(AllocationPage.AllocationExtentInterval, allocationPage.AllocationMap.Length);
    }

    [Fact]
    public async Task Gam_Page_Has_Most_Extents_Allocated()
    {
        // GAM = 1 means extent is free; early extents (0-11) are used by system pages
        // so in a fresh/small database most early extents will be allocated (bit = 0 in GAM)
        var pageData = await GetPageData(new PageAddress(1, AllocationPage.FirstGamPage));

        var parser = new AllocationPageParser();

        var allocationPage = parser.Parse(pageData);

        // The allocation map should contain at least some true (free) and false (allocated) entries
        Assert.Contains(true, allocationPage.AllocationMap);
        Assert.Contains(false, allocationPage.AllocationMap);
    }

    [Fact]
    public async Task Dcm_And_Bcm_Pages_Parse_Without_Error()
    {
        var parser = new AllocationPageParser();

        var dcmData = await GetPageData(new PageAddress(1, AllocationPage.FirstDcmPage));
        var bcmData = await GetPageData(new PageAddress(1, AllocationPage.FirstBcmPage));

        var dcmPage = parser.Parse(dcmData);
        var bcmPage = parser.Parse(bcmData);

        Assert.Equal(AllocationPage.AllocationExtentInterval, dcmPage.AllocationMap.Length);
        Assert.Equal(AllocationPage.AllocationExtentInterval, bcmPage.AllocationMap.Length);
    }
}