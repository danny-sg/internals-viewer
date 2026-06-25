using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

public class IamPageParserTests(ITestOutputHelper testOutput)
    : PageParserTestsBase(testOutput)
{
    [Theory]
    [InlineData(100)]
    public async Task Can_Parse_Iam_Page(int pageId)
    {
        var pageData = await GetPageData(new PageAddress(1, pageId));

        var parser = new IamPageParser();

        var page = parser.Parse(pageData);

        Assert.Equal(PageType.Iam, page.PageHeader.PageType);
        Assert.Equal(new PageAddress(1, 0), page.StartPage);

        Assert.Equal(new PageAddress(1, 99), page.SinglePageSlots[0]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[1]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[2]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[3]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[4]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[5]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[6]);
        Assert.Equal(new PageAddress(0, 0), page.SinglePageSlots[7]);
    }

    [Fact]
    public async Task Allocation_Map_Has_Correct_Length()
    {
        var pageData = await GetPageData(new PageAddress(1, 100));

        var parser = new IamPageParser();

        var page = parser.Parse(pageData);

        Assert.Equal(AllocationPage.AllocationInterval, page.AllocationMap.Length);
    }

    [Fact]
    public async Task Single_Page_Slots_Has_Eight_Entries()
    {
        var pageData = await GetPageData(new PageAddress(1, 100));

        var parser = new IamPageParser();

        var page = parser.Parse(pageData);

        Assert.Equal(AllocationPage.SlotCount, page.SinglePageSlots.Length);
    }

    [Fact]
    public async Task Allocation_Map_Has_Some_Allocated_Extents()
    {
        var pageData = await GetPageData(new PageAddress(1, 100));

        var parser = new IamPageParser();

        var page = parser.Parse(pageData);

        // At minimum the extent containing page 99 should be allocated
        Assert.Contains(true, page.AllocationMap);
    }
}