using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class ColumnstoreServiceTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string DumpPath = @"C:\ColumnstoreDump";

    [Fact]
    public async Task Can_Get_ColumnStore_Index()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var lobDataService = new LobDataService(pageService);

        var service = new ColumnstoreService(dataReader, lobDataService);

        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(f => f.TableName == "Sales");

        var index = await service.GetIndex(allocationUnit!, database, CancellationToken.None);

        Assert.NotNull(index);

        var pointer = index.RowGroups[0].Segments[0].DataPointer;

        var data = await service.GetData(database, pointer, CancellationToken.None);
    }

    [Fact]
    public async Task Can_Dump_Segment_Data()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var lobDataService = new LobDataService(pageService);

        var service = new ColumnstoreService(dataReader, lobDataService);

        Directory.CreateDirectory(DumpPath);

        foreach (var table in new[] { "Sales", "SegConstant", "SegSequential", "SegDictionary", "SegTypes", "SegNulls", "SegArchive", "SegWide", "SegManyRuns", "SegTiny", "SegVeryWide", "SegKeyedNulls", "SegLen400", "SegUnicode", "SegDecSeq", "SegDecTiny", "SegDecNeg", "SegDecScale" })
        {
            var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == table);

            if (allocationUnit is null)
            {
                continue;
            }

            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                await DumpTable(service, database, table, rowGroup.RowGroupId);
            }
        }

        await DumpTable(service, database, "SegDictionary", 1);
    }

    private async Task DumpTable(ColumnstoreService service, DatabaseSource database, string tableName, int rowGroupId)
    {
        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == tableName);

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var rowGroup = index.RowGroups.First(r => r.RowGroupId == rowGroupId);

        foreach (var segment in rowGroup.Segments)
        {
            var data = await service.GetData(database, segment.DataPointer, CancellationToken.None);

            TestOutput.WriteLine($"{tableName} rg {rowGroupId} col {segment.Key.ColumnId} "
                                 + $"enc {segment.Encoding} rows {segment.RowCount} "
                                 + $"onDiskSize {segment.OnDiskSize} length {data.Length}");

            await File.WriteAllBytesAsync(Path.Combine(DumpPath, $"{tableName}_rg{rowGroupId}_col{segment.Key.ColumnId}_seg.bin"), data);

            if (segment.LocalDictionary is { } local)
            {
                await DumpDictionary(service, database, tableName, segment.Key.ColumnId, local, "local");
            }
        }

        foreach (var column in index.Columns.Where(c => c.GlobalDictionary is not null))
        {
            await DumpDictionary(service, database, tableName, column.ColumnStoreColumnId, column.GlobalDictionary!, "global");
        }
    }

    private async Task DumpDictionary(ColumnstoreService service,
                                      DatabaseSource database,
                                      string tableName,
                                      int columnId,
                                      SegmentDictionary dictionary,
                                      string scope)
    {
        var data = await service.GetData(database, dictionary.DataPointer, CancellationToken.None);

        TestOutput.WriteLine($"{tableName} col {columnId} {scope} dictionary {dictionary.DictionaryId} "
                             + $"entries {dictionary.EntryCount} onDiskSize {dictionary.OnDiskSize} length {data.Length}");

        await File.WriteAllBytesAsync(Path.Combine(DumpPath, $"{tableName}_col{columnId}_dict{dictionary.DictionaryId}_{scope}.bin"), data);
    }

    [Theory]
    [InlineData("Sales", 0)]
    [InlineData("Sales", 1)]
    [InlineData("SegDictionary", 1)]
    [InlineData("SegConstant", 0)]
    [InlineData("SegSequential", 0)]
    public async Task Segment_Blob_Matches_Catalog_Metadata(string tableName, int rowGroupId)
    {
        var (service, database) = await CreateService();

        var index = await service.GetIndex(database.AllocationUnits.Values.First(a => a.TableName == tableName),
                                           database,
                                           CancellationToken.None);

        var rowGroup = index.RowGroups.First(r => r.RowGroupId == rowGroupId);

        Assert.NotEmpty(rowGroup.Segments);

        foreach (var segment in rowGroup.Segments)
        {
            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            Assert.Equal(segment.OnDiskSize, blob.ExpectedSize);
            Assert.Equal(segment.RowCount, blob.RowCount);
            Assert.Equal(1, blob.Version);

            var stream = new SegmentDataIdStream(blob);

            var ids = stream.ReadAll().ToList();

            Assert.Equal(segment.RowCount, ids.Count);

            Assert.All(Enumerable.Range(0, Math.Min(ids.Count, 5000)),
                       i => Assert.Equal(ids[i], stream.GetDataId(i)));

            Assert.Equal(ids[^1], stream.GetDataId(ids.Count - 1));
        }
    }

    [Theory]
    [InlineData("Sales", 0, 4, "Region")]
    [InlineData("Sales", 0, 5, "ProductCode")]
    [InlineData("SegDictionary", 1, 2, "Val")]
    public async Task Decoded_Segment_Matches_Queried_Values(string tableName, int rowGroupId, int columnId, string columnName)
    {
        var (service, database) = await CreateService();

        var index = await service.GetIndex(database.AllocationUnits.Values.First(a => a.TableName == tableName),
                                           database,
                                           CancellationToken.None);

        var segment = index.RowGroups.First(r => r.RowGroupId == rowGroupId)
                           .Segments.First(s => s.Key.ColumnId == columnId);

        var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

        var decoded = reader.ReadAll()
                            .GroupBy(v => (string?)v)
                            .ToDictionary(g => g.Key!, g => g.Count());

        var expected = await QueryDistinct(tableName, columnName);

        Assert.Equal(expected.OrderBy(v => v), decoded.Keys.OrderBy(v => v));
        Assert.Equal(segment.RowCount, decoded.Values.Sum());

        TestOutput.WriteLine($"{tableName}.{columnName} rg {rowGroupId}: {decoded.Count} distinct over {segment.RowCount} rows");
    }

    private async Task<(ColumnstoreService Service, DatabaseSource Database)> CreateService()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        return (new ColumnstoreService(dataReader, new LobDataService(pageService)), database);
    }

    private static async Task<List<string>> QueryDistinct(string tableName, string columnName)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT DISTINCT CAST([{columnName}] AS varchar(100)) FROM [{tableName}]";

        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();

        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
