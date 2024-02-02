using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

public class PfsPageParserTests(ITestOutputHelper testOutput)
    : PageParserTestsBase(testOutput)
{
    [Fact]
    public async Task Can_Parse_Pfs_Page()
    {
        var pageData = await GetPageData(new PageAddress(1, 1));

        var parser = new PfsPageParser();

        var page = parser.Parse(pageData);

        Assert.Equal(new PfsByte { IsAllocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, IsIam = false, IsMixed = false },
            page.PfsBytes[0]);

        Assert.Equal(new PfsByte { IsAllocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, IsIam = false, IsMixed = false },
            page.PfsBytes[1]);

        Assert.Equal(new PfsByte { IsAllocated = true, PageSpaceFree = SpaceFree.Empty, IsIam = true, IsMixed = true },
            page.PfsBytes[100]);
    }
}