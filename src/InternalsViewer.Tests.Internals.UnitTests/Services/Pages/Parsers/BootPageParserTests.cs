using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Services.Pages.Parsers;

public class BootPageParserTests(ITestOutputHelper testOutput)
    : PageParserTestsBase(testOutput)
{
    [Fact]
    public async Task Can_Parse_Boot_Page()
    {
        var pageData = await GetPageData(BootPage.BootPageAddress);

        var parser = new BootPageParser();

        var page = parser.Parse(pageData);

        //dbi_version = 957
        Assert.Equal(957, page.CurrentVersion);

        //dbi_createVersion = 904
        Assert.Equal(904, page.CreatedVersion);

        // dbi_cmptlevel = 160
        Assert.Equal(160, page.CompatibilityLevel);

        // dbi_dbid = 9
        Assert.Equal(9, page.DatabaseId);

        // dbi_maxLogSpaceUsed = 19361792
        Assert.Equal(19361792, page.MaxLogSpaceUsed);

        // dbi_crdate = 2023 - 12 - 12 12:26:16.003
        Assert.Equal(new DateTime(2023, 12, 12, 12, 26, 16, 3), page.CreatedDateTime);

        // dbi_dbname = AdventureWorks2022
        Assert.Equal("AdventureWorks2022", page.DatabaseName);

        // dbi_collation = 872468488 
        Assert.Equal(872468488, page.Collation);

        // dbi_status = 0x00810008
        Assert.Equal(0x00810008, page.Status);

        Assert.Equal(new PageAddress(1, 20), page.FirstAllocationUnitsPage);

        Assert.Equal(1099511628357, page.NextAllocationUnitId);
    }
}