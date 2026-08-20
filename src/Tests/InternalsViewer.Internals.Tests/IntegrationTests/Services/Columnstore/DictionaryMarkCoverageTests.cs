using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Interfaces.Annotations;
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

public sealed class DictionaryMarkCoverageTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Report_Unmarked_Bytes()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == "Sales");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var metadata = index.Columns.Select(c => c.GlobalDictionary).First(d => d is { ColumnId: 4 });

        var blob = (StringDictionary)await service.GetDictionaryBlob(database, metadata!, CancellationToken.None, isMarkEnabled: true);

        var covered = new bool[blob.Data.Length];

        void Cover(IDataStructure structure, string label)
        {
            foreach (var item in structure.MarkItems.OfType<PropertyItem>().Where(i => i.Offset >= 0))
            {
                TestOutput.WriteLine($"  {label,-18} {item.PropertyName,-22} 0x{item.Offset:X2}..0x{item.Offset + item.Length - 1:X2}");

                for (var i = item.Offset; i < item.Offset + item.Length && i < covered.Length; i++)
                {
                    covered[i] = true;
                }
            }
        }

        TestOutput.WriteLine($"blob {blob.Data.Length} bytes, {blob.Handles.Length} handles, {blob.Pages.Length} pages");

        Cover(blob, "dictionary");
        Cover(blob.Store, "store");
        Cover(blob.HandleArray, "handle array");
        Cover(blob.PageSizeArray, "page size array");

        foreach (var page in blob.Pages)
        {
            Cover(page, "page");
        }

        TestOutput.WriteLine(string.Empty);
        TestOutput.WriteLine("Unmarked runs:");

        var start = -1;

        for (var i = 0; i <= covered.Length; i++)
        {
            var isCovered = i < covered.Length && covered[i];

            if (!isCovered && start < 0)
            {
                start = i;
            }
            else if (isCovered && start >= 0)
            {
                TestOutput.WriteLine($"  0x{start:X2}..0x{i - 1:X2}  ({i - start} bytes)");

                start = -1;
            }
        }

        if (start >= 0)
        {
            TestOutput.WriteLine($"  0x{start:X2}..0x{covered.Length - 1:X2}  ({covered.Length - start} bytes)");
        }
    }
}
