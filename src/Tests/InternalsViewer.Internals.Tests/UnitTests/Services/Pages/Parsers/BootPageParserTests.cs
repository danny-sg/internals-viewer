using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

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

        Assert.Equal(36, page.DatabaseNameLength);

        Assert.Equal(64000, page.MaxDatabaseTimestamp);

        Assert.Equal(new LogSequenceNumber(53, 31269, 37), page.CheckpointLsn);

        Assert.Equal(new LogSequenceNumber(53, 31269, 37), page.DirtyPageLsn);

        Assert.Equal(new LogSequenceNumber(18, 65, 69), page.LatestVersioningUpgradeLsn);

        Assert.Equal(2, page.DbccFlags);

        Assert.Equal(0x1948, page.LastTransactionId);

        Assert.Equal(0x75813000, page.ReleaseStatus);

        Assert.Equal(new DateTime(2023, 5, 8, 12, 7, 29, 53), page.ModifiedDateTime);

        Assert.Equal(0x10000451, page.ResourceDatabaseVersion);

        Assert.Equal(Guid.Parse("3e94febd-98ca-424f-8787-5dd24a6e3976"), page.FamilyGuid);

        Assert.Equal(Guid.Parse("c351c5a4-7c86-4046-bbcd-ee049c76d65a"), page.RecoveryForkGuid);

        Assert.Equal(Guid.Parse("104bd1e5-5729-481c-818b-04c0e3216e68"), page.ServiceBrokerGuid);

        Assert.Equal(0, page.ServiceBrokerOptions);

        Assert.Equal(72057594042908672, page.PersistentVersionStoreRowsetId);

        Assert.Equal(72057594042974208, page.PersistentVersionStoreLongTermRowsetId);
    }
}