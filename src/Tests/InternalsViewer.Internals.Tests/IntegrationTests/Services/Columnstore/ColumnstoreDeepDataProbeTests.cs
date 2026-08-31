using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

[Trait("Category", "Integration")]
[Trait("Area", "Columnstore")]
public sealed class ColumnstoreDeepDataProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Theory]
    [InlineData("Sales")]
    [InlineData("SegTypes")]
    public async Task Probe_Deep_Data(string tableName)
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(dataReader, new LobDataService(pageService));

        var index = await service.GetIndex(database.AllocationUnits.Values.First(a => a.TableName == tableName),
                                           database,
                                           CancellationToken.None);

        foreach (var segment in index.CompressedRowGroups.First().Segments)
        {
            var structure = segment.Column?.Structure;

            var min = Describe(segment.MinDeepData, structure);

            var max = Describe(segment.MaxDeepData, structure);

            TestOutput.WriteLine($"PROBE {tableName} col {segment.Key.ColumnId} {segment.Column?.Name} "
                                 + $"type {structure?.DataType} enc {segment.Encoding} "
                                 + $"minId {segment.MinDataId} maxId {segment.MaxDataId} "
                                 + $"minDeep [{min}] maxDeep [{max}]");
        }

        Assert.NotEmpty(index.RowGroups);
    }

    private static string Describe(byte[]? data, Internals.Metadata.Structures.ColumnStructure? structure)
    {
        if (data is null || data.Length == 0)
        {
            return "empty";
        }

        var hex = Convert.ToHexString(data);

        if (structure is null)
        {
            return hex;
        }

        try
        {
            var length = BitConverter.ToUInt16(data, 0);

            var value = DataConverter.GetValue(data.AsSpan(2, length),
                                               structure.DataType,
                                               structure.Precision,
                                               structure.Scale);

            return $"len {length} => {value}";
        }
        catch (Exception exception)
        {
            return $"{hex} => {exception.GetType().Name}";
        }
    }
}
