using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class StringDictionaryHeaderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Reads_The_String_Store_And_Array_Headers()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var checkedAny = false;

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")
                                                                                 || a.TableName == "Sales"))
        {
            ColumnStoreIndex index;

            try
            {
                index = await service.GetIndex(allocationUnit, database, CancellationToken.None);
            }
            catch
            {
                continue;
            }

            foreach (var metadata in index.Columns.Select(c => c.GlobalDictionary).Where(d => d is not null))
            {
                if (await service.GetDictionaryBlob(database, metadata!, CancellationToken.None) is not StringDictionary strings)
                {
                    continue;
                }

                checkedAny = true;

                TestOutput.WriteLine($"{allocationUnit.TableName} column {metadata!.ColumnId} "
                                     + $"maxStringSize {strings.MaxStringSize} stringCount {strings.StringCount} "
                                     + $"entries {metadata.EntryCount} handles {strings.HandleCount} pages {strings.PageCount}");

                Assert.Equal(SubLobType.StringStore, strings.Store.SubLobType);
                Assert.Equal(SubLobType.Array, strings.HandleArray.SubLobType);
                Assert.Equal(SubLobType.Array, strings.PageSizeArray.SubLobType);

                Assert.Equal(8192, strings.MaxStringSize);
                Assert.Equal(metadata.EntryCount - 1, strings.StringCount);

                Assert.Equal(8, strings.HandleArray.ElementSize);
                Assert.Equal(metadata.EntryCount, strings.HandleCount);
                Assert.Equal(4, strings.PageSizeArray.ElementSize);
            }
        }

        Assert.True(checkedAny);
    }
}
