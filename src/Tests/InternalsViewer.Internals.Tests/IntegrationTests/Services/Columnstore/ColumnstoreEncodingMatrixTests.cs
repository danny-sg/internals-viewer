using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports every combination of encoding, blob layout and sub lob type the lab tables actually produce
/// </summary>
public sealed class ColumnstoreEncodingMatrixTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Report_Encoding_Matrix()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var rows = new SortedSet<string>();

        foreach (var allocationUnit in database.AllocationUnits.Values.GroupBy(a => a.TableName).Select(g => g.First()))
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

            foreach (var segment in index.CompressedRowGroups.SelectMany(r => r.Segments))
            {
                try
                {
                    rows.Add(await Describe(service, database, segment));
                }
                catch (Exception exception)
                {
                    TestOutput.WriteLine($"skip {allocationUnit.TableName} col {segment.Key.ColumnId}: {exception.Message}");
                }
            }
        }

        foreach (var row in rows)
        {
            TestOutput.WriteLine(row);
        }
    }

    private static async Task<string> Describe(ColumnstoreService service, Engine.Database.DatabaseSource database, ColumnSegment segment)
    {
        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

        var store = blob.ValueStore is { } valueStore
            ? $"{valueStore.SubLobType}/{string.Join("+", valueStore.Pages.Select(p => p.SubLobType.ToString()).Distinct())}"
            : "-";

        var metadata = segment.SecondaryDictionaryId >= 0 ? segment.LocalDictionary : segment.Column?.GlobalDictionary;

        var dictionary = "-";

        if (metadata is not null)
        {
            var parsed = await service.GetDictionaryBlob(database, metadata, CancellationToken.None);

            dictionary = parsed switch
            {
                StringDictionary strings
                    => $"{parsed.LobType}/{strings.SubLobType}/"
                       + $"{string.Join("+", strings.Pages.Select(p => p.SubLobType.ToString()).Distinct())}",
                NumericDictionary numeric
                    => $"{parsed.LobType}/{numeric.SubLobType}+{numeric.ValueSubLobType}",
                _ => $"{parsed.LobType}"
            };
        }

        return $"{(int)segment.Encoding} {segment.Encoding,-24} | {blob.StructureType,-12} "
               + $"| rle {(blob.Header.HasRleArray ? "yes" : "NO "),-4} entries {blob.RleEntryCount,-7} "
               + $"| bitpack {(blob.Header.HasBitpackArray ? "yes" : "NO "),-4} units {blob.BitpackUnitCount,-7} "
               + $"| store {store,-28} | dict {dictionary}";
    }
}
