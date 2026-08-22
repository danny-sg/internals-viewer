using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Chains;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Walks a delta store the way the viewer does, from its row group's hobt to the records on its pages
/// </summary>
public sealed class DeltaStoreProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegDelta";

    [RequiresConnectionStringFact("local")]
    public async Task Reads_Delta_Store_Pages()
    {
        await Build();

        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var columnstore = new ColumnstoreService(reader, new LobDataService(pageService));

        var indexUnit = database.AllocationUnits.Values.First(a => a.TableName == TableName);

        var index = await columnstore.GetIndex(indexUnit, database, CancellationToken.None);

        var deltaHobt = index.RowGroups.Select(r => r.DeltaStoreHobtId).FirstOrDefault(h => h != 0);

        TestOutput.WriteLine($"row groups {index.RowGroups.Count}, delta store hobt {deltaHobt}");

        Assert.NotEqual(0, deltaHobt);

        // The delta store is a rowset of its own, so it is found by its hobt rather than by the index it belongs to
        var allocationUnit = database.AllocationUnits
                                     .Values
                                     .FirstOrDefault(a => a.PartitionId == deltaHobt
                                                          && a.AllocationUnitType == AllocationUnitType.InRowData);

        Assert.NotNull(allocationUnit);

        TestOutput.WriteLine($"allocation unit {allocationUnit.AllocationUnitId} hobt {allocationUnit.PartitionId} "
                             + $"index {allocationUnit.IndexId} first {allocationUnit.FirstPage} "
                             + $"iam {allocationUnit.FirstIamPage}");

        var iam = await new IamChainService(pageService).LoadChain(database,
                                                               allocationUnit.FirstIamPage,
                                                               CancellationToken.None);

        var pages = 0;

        var dataPages = 0;

        foreach (var (from, to) in iam.GetAllocatedPageRanges(allocationUnit.FirstIamPage.FileId))
        {
            for (var pageId = from; pageId <= to && pages < 40; pageId++)
            {
                pages++;

                var page = await pageService.GetPage(database,
                                                 new PageAddress(allocationUnit.FirstIamPage.FileId, pageId),
                                                 CancellationToken.None,
                                                 isMarkEnabled: false);

                if (page.PageHeader.PageType == PageType.Data)
                {
                    dataPages++;

                    if (dataPages == 1)
                    {
                        TestOutput.WriteLine($"first data page {page.PageAddress} slots {page.PageHeader.SlotCount} "
                                             + $"free {page.PageHeader.FreeCount}");
                    }
                }
            }
        }

        TestOutput.WriteLine($"{pages} pages walked, {dataPages} data pages");

        Assert.True(dataPages > 0);
    }

    /// <summary>
    /// A load under the delta store threshold and without a table lock stays uncompressed
    /// </summary>
    private static async Task Build()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{TableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        await Execute(connection, $"CREATE TABLE {TableName} (Id int NOT NULL, Note varchar(40) NOT NULL)");

        await Execute(connection, $"CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName}");

        await Execute(connection,
                      $"""
                       INSERT INTO {TableName} (Id, Note)
                       SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 'delta store row'
                       FROM sys.all_columns a CROSS JOIN sys.all_columns b
                       """);

        await Execute(connection, "CHECKPOINT");
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await command.ExecuteNonQueryAsync();
    }
}
