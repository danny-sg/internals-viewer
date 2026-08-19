using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Dumps the header of every numeric dictionary, to place the fields the parser does not yet read
/// </summary>
public sealed class NumericDictionaryHeaderProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Dump_Numeric_Dictionary_Headers()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput), pageService, loader);

        var service = new ColumnstoreService(dataReader, new LobDataService(pageService));

        foreach (var allocationUnit in database.AllocationUnits.Values.GroupBy(a => a.TableName).Select(g => g.First()))
        {
            try
            {
                var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

                foreach (var metadata in index.Columns.Select(c => c.GlobalDictionary).Where(d => d is not null))
                {
                    var blob = await service.GetDictionaryBlob(database, metadata!, CancellationToken.None);

                    if (blob is not NumericDictionary numeric)
                    {
                        continue;
                    }

                    TestOutput.WriteLine($"{allocationUnit.TableName} column {metadata!.ColumnId} "
                                         + $"dictionary {metadata.DictionaryId} entries {numeric.EntryCount}");

                    for (var offset = 0; offset < NumericDictionary.HeaderSize; offset += 4)
                    {
                        var value = BitConverter.ToInt32(blob.Data.Span.Slice(offset, 4));

                        TestOutput.WriteLine($"    0x{offset:X2} ({offset,2}) = {value}");
                    }
                }
            }
            catch
            {
                // Not every allocation unit is a columnstore index
            }
        }
    }
}
