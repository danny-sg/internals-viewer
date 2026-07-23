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

        AssertPfsByte(page.PfsBytes[0], true, SpaceFree.OneHundredPercent, false, false);

        AssertPfsByte(page.PfsBytes[1], true, SpaceFree.OneHundredPercent, false, false);

        AssertPfsByte(page.PfsBytes[100], true, SpaceFree.Empty, true, true);
    }

    private static void AssertPfsByte(byte value,
                                      bool expectedIsAllocated,
                                      SpaceFree expectedSpaceFree,
                                      bool expectedIsIam,
                                      bool expectedIsMixed)
    {
        var pfsByte = new PfsByte(value);

        Assert.Equal(expectedIsAllocated, pfsByte.IsAllocated);
        Assert.Equal(expectedSpaceFree, pfsByte.PageSpaceFree);
        Assert.Equal(expectedIsIam, pfsByte.IsIam);
        Assert.Equal(expectedIsMixed, pfsByte.IsMixed);
    }
}
