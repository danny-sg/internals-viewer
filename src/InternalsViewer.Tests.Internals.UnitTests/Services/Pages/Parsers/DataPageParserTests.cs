using InternalsViewer.Internals.Services.Pages.Parsers;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Pages.Parsers;

public class DataPageParserTests(ITestOutputHelper testOutput)
    : PageParserTestsBase(testOutput)
{
    [Fact]
    public async Task Can_Parse_Data_Page()
    {
        var pageData = await GetPageData(new PageAddress(1, 25520));

        var parser = new DataPageParser();

        var page = parser.Parse(pageData);

        Assert.Equal(96, page.OffsetTable[0]);
        Assert.Equal(110, page.OffsetTable[1]);
        Assert.Equal(124, page.OffsetTable[2]);
        Assert.Equal(138, page.OffsetTable[3]);
        Assert.Equal(152, page.OffsetTable[4]);
        Assert.Equal(166, page.OffsetTable[5]);
        Assert.Equal(180, page.OffsetTable[6]);
        Assert.Equal(194, page.OffsetTable[7]);
        Assert.Equal(208, page.OffsetTable[8]);
        Assert.Equal(222, page.OffsetTable[9]);
        Assert.Equal(236, page.OffsetTable[10]);

        Assert.Equal(5696, page.OffsetTable[400]);
        Assert.Equal(5710, page.OffsetTable[401]);
        Assert.Equal(5724, page.OffsetTable[402]);
        Assert.Equal(5738, page.OffsetTable[403]);
        Assert.Equal(5752, page.OffsetTable[404]);
        Assert.Equal(5766, page.OffsetTable[405]);
        Assert.Equal(5780, page.OffsetTable[406]);
        Assert.Equal(5794, page.OffsetTable[407]);
        Assert.Equal(5808, page.OffsetTable[408]);
        Assert.Equal(5822, page.OffsetTable[409]);
        Assert.Equal(5836, page.OffsetTable[410]);
        Assert.Equal(5850, page.OffsetTable[411]);
        Assert.Equal(5864, page.OffsetTable[412]);

        Assert.Equal(page.PageHeader.SlotCount, page.OffsetTable.Count);
    }
}