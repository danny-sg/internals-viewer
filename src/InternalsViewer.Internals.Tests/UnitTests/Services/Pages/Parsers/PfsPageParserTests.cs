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

        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, Iam = false, Mixed = false },
            page.PfsBytes[0]);

        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.OneHundredPercent, Iam = false, Mixed = false },
            page.PfsBytes[1]);

        Assert.Equal(new PfsByte { Allocated = true, PageSpaceFree = SpaceFree.Empty, Iam = true, Mixed = true },
            page.PfsBytes[100]);
    }
}