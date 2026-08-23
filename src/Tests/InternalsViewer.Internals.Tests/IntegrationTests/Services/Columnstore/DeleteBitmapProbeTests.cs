using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Chains;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Dumps the raw records of a delete bitmap so its row layout can be read off the bytes
/// </summary>
public sealed class DeleteBitmapProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegDelMap";

    /// <summary>
    /// A row group with three rows deleted at known positions, so a record can be matched to the row it marks
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

        await Execute(connection, $"CREATE TABLE {TableName} (Id int NOT NULL, Note varchar(20) NOT NULL)");

        await Execute(connection, $"CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName}");

        // Two batches compressed separately, so each becomes a row group of its own
        for (var batch = 0; batch < 2; batch++)
        {
            await Execute(connection,
                          $"""
                           INSERT INTO {TableName} (Id, Note)
                           SELECT TOP (10000) {batch * 10000} + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 'row'
                           FROM sys.all_columns a CROSS JOIN sys.all_columns b
                           """);

            await Execute(connection,
                          $"ALTER INDEX CCI_{TableName} ON {TableName} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)");
        }

        // One from the first row group, two from the second
        await Execute(connection, $"DELETE FROM {TableName} WHERE Id IN (5, 10001, 19999)");

        await Execute(connection, "CHECKPOINT");
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await command.ExecuteNonQueryAsync();
    }

    [RequiresConnectionStringFact("local")]
    public async Task Report_Raw_Records()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await Build();

        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        foreach (var candidate in database.AllocationUnits.Values.Where(a => a.TableName == TableName))
        {
            TestOutput.WriteLine($"candidate hobt {candidate.PartitionId} part {candidate.PartitionNumber} "
                                 + $"owner {candidate.OwnerType} type {candidate.AllocationUnitType} "
                                 + $"unit {candidate.AllocationUnitId}");
        }

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == TableName);

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        Assert.NotNull(index.DeleteBitmap);

        var bitmap = index.DeleteBitmap!;

        TestOutput.WriteLine($"delete bitmap hobt {bitmap.HobtId} allocated {bitmap.IsAllocated} "
                             + $"first {bitmap.FirstPage} root {bitmap.RootPage} iam {bitmap.FirstIamPage} "
                             + $"units {bitmap.AllocationUnits.Count}");

        foreach (var unit in bitmap.AllocationUnits)
        {
            TestOutput.WriteLine($"  unit {unit.AllocationUnitId} {unit.AllocationUnitType} "
                                 + $"first {unit.FirstPage} root {unit.RootPage} iam {unit.FirstIamPage} "
                                 + $"used {unit.UsedPages} total {unit.TotalPages}");
        }

        var chain = await new IamChainService(pageService).LoadChain(database,
                                                                     bitmap.FirstIamPage,
                                                                     CancellationToken.None);

        var dataUnit = bitmap.DataAllocationUnit!;

        var deleted = new List<(long Group, long Row)>();

        foreach (var (from, to) in chain.GetAllocatedPageRanges(bitmap.FirstIamPage.FileId))
        {
            for (var pageId = from; pageId <= to; pageId++)
            {
                var page = await pageService.GetPage(database,
                                                     new PageAddress(bitmap.FirstIamPage.FileId, pageId),
                                                     CancellationToken.None,
                                                     isMarkEnabled: false);

                // A mixed extent holds pages of other objects, so the IAM's ranges are not the rowset on their own
                if (page is not InternalsViewer.Internals.Engine.Pages.AllocationUnitPage dataPage
                    || page.PageHeader.AllocationUnitId != dataUnit.AllocationUnitId
                    || page.PageHeader.PageType != PageType.Data
                    || page.PageHeader.Level != 0)
                {
                    continue;
                }

                var pageRecords = ServiceHelper.CreateRecordService(TestOutput).GetRecords(dataPage).ToList();

                TestOutput.WriteLine($"  page {dataPage.PageAddress} slots {dataPage.PageHeader.SlotCount} "
                                     + $"records {pageRecords.Count}");

                foreach (var record in pageRecords)
                {
                    var fields = record.Fields.Select(f => f.Value).ToList();

                    deleted.Add((long.Parse(fields[0]) >> 1, long.Parse(fields[1])));
                }
            }
        }

        TestOutput.WriteLine(string.Join(", ", deleted.Select(d => $"({d.Group},{d.Row})")));

        // Taken from the engine rather than pinned, the tuple mover being free to merge deletes away at any time
        await using var counts = new SqlCommand($"""
                                                 SELECT SUM(deleted_rows)
                                                 FROM sys.dm_db_column_store_row_group_physical_stats rg
                                                 JOIN sys.tables t ON t.object_id = rg.object_id
                                                 WHERE t.name = '{TableName}'
                                                 """, connection);

        var expected = Convert.ToInt32(await counts.ExecuteScalarAsync());

        TestOutput.WriteLine($"engine reports {expected} deleted rows, decoded {deleted.Count}");

        Assert.Equal(expected, deleted.Count);

        Assert.All(deleted, d => Assert.InRange(d.Row, 0, int.MaxValue));
    }
}
