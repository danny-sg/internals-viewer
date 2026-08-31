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

/// <summary>
/// Reports which columnstore tables carry a local dictionary, a local one being built per row group
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "Columnstore")]
public sealed class ColumnstoreDictionaryScopeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Report_Dictionary_Scope_Across_Tables()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(dataReader, new LobDataService(pageService));

        var columnstore = database.AllocationUnits
                                  .Values
                                  .Where(a => a.IndexName is not null || a.TableName is not null)
                                  .GroupBy(a => a.TableName)
                                  .Select(g => g.First())
                                  .ToList();

        foreach (var allocationUnit in columnstore)
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

            var segments = index.CompressedRowGroups.SelectMany(r => r.Segments).ToList();

            if (segments.Count == 0)
            {
                continue;
            }

            var local = segments.Where(s => s.SecondaryDictionaryId >= 0).ToList();

            var global = index.Columns.Count(c => c.GlobalDictionary is not null);

            TestOutput.WriteLine($"{allocationUnit.TableName}: {segments.Count} segments, "
                                 + $"{global} global dictionaries, {local.Count} segments with a local dictionary");

            foreach (var dictionary in index.Columns.Select(c => c.GlobalDictionary).Where(d => d is not null))
            {
                TestOutput.WriteLine($"    dictionary {dictionary!.DictionaryId} type {dictionary.Type} "
                                     + $"flags {dictionary.Flags} "
                                     + $"unmapped [{string.Join(", ", (dictionary.UnmappedFields ?? []).Select(f => $"{f.Key}={f.Value.Length}b:0x{Convert.ToHexString(f.Value)}"))}]");
            }

            foreach (var segment in local)
            {
                TestOutput.WriteLine($"    row group {segment.Key.RowGroupId} column {segment.Key.ColumnId} "
                                     + $"-> local dictionary {segment.SecondaryDictionaryId}");
            }
        }
    }
}
