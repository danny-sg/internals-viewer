using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

[Trait("Category", "Integration")]
[Trait("Area", "Columnstore")]
public sealed class ColumnstoreStructureDumpTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string DumpPath = @"C:\ColumnstoreDump\Structures";

    [Theory]
    [InlineData("Sales")]
    [InlineData("SegDeletes")]
    [InlineData("SegDelta")]
    public async Task Dump_Index_Structure(string tableName)
    {
        var (service, database) = await CreateService();

        var index = await GetIndex(service, database, tableName);

        Write($"{tableName}_index.txt", ColumnstoreStructureDumper.DumpIndex(index));
    }

    [Theory]
    [InlineData("Sales", 0)]
    [InlineData("SegTypes", 0)]
    [InlineData("SegNulls", 0)]
    public async Task Dump_RowGroup_Summary(string tableName, int rowGroupId)
    {
        var (service, database) = await CreateService();

        var index = await GetIndex(service, database, tableName);

        var report = ColumnstoreStructureDumper.DumpRowGroup(index.RowGroups.First(r => r.RowGroupId == rowGroupId));

        Write($"{tableName}_rg{rowGroupId}_rowgroup.txt", report);
    }

    [Theory]
    [InlineData("Sales", 0, 4)]
    [InlineData("Sales", 0, 5)]
    [InlineData("Sales", 0, 2)]
    [InlineData("SegConstant", 0, 2)]
    [InlineData("SegSequential", 0, 2)]
    [InlineData("SegTiny", 0, 2)]
    [InlineData("SegNulls", 0, 2)]
    public async Task Dump_Segment_Structure(string tableName, int rowGroupId, int columnId)
    {
        var (service, database) = await CreateService();

        var index = await GetIndex(service, database, tableName);

        var segment = index.RowGroups.First(r => r.RowGroupId == rowGroupId)
                           .Segments.First(s => s.Key.ColumnId == columnId);

        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

        var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

        var report = ColumnstoreStructureDumper.DumpSegment(segment, blob)
                     + Environment.NewLine
                     + ColumnstoreStructureDumper.DumpDecodedRows(segment, reader, 20);

        Write($"{tableName}_rg{rowGroupId}_col{columnId}_segment.txt", report);
    }

    [Theory]
    [InlineData("Sales", 4)]
    [InlineData("Sales", 5)]
    [InlineData("Sales", 7)]
    [InlineData("SegDictionary", 2)]
    [InlineData("SegNonAscii", 3)]
    [InlineData("SegNonAscii", 4)]
    [InlineData("SegNonAscii", 5)]
    public async Task Dump_Dictionary_Structure(string tableName, int columnId)
    {
        var (service, database) = await CreateService();

        var index = await GetIndex(service, database, tableName);

        var column = index.Columns.First(c => c.ColumnStoreColumnId == columnId);

        var metadata = column.GlobalDictionary
                       ?? index.CompressedRowGroups
                               .SelectMany(r => r.Segments)
                               .First(s => s.Key.ColumnId == columnId && s.LocalDictionary is not null)
                               .LocalDictionary!;

        var blob = await service.GetDictionaryBlob(database, metadata, CancellationToken.None, column: column.Structure);

        Write($"{tableName}_col{columnId}_dict{metadata.DictionaryId}.txt",
              ColumnstoreStructureDumper.DumpDictionary(metadata, blob));
    }

    private void Write(string fileName, string report)
    {
        Directory.CreateDirectory(DumpPath);

        var path = Path.Combine(DumpPath, fileName);

        File.WriteAllText(path, report);

        TestOutput.WriteLine(path);
        TestOutput.WriteLine(string.Empty);
        TestOutput.WriteLine(report);

        Assert.NotEmpty(report);
    }

    private static async Task<ColumnStoreIndex> GetIndex(ColumnstoreService service, DatabaseSource database, string tableName)
    {
        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == tableName);

        return await service.GetIndex(allocationUnit, database, CancellationToken.None);
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
}
