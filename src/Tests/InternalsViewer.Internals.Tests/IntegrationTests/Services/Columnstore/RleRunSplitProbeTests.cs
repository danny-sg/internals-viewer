using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class RleRunSplitProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Probe_Rle_Run_Split()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var targets = new[] { ("Sales", 0, 5), ("Sales", 0, 8), ("Sales", 0, 4), ("SegNulls", 0, 3), ("SegTypes", 0, 2) };

        var lines = new List<string>();

        foreach (var (tableName, rowGroupId, columnId) in targets)
        {
            var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

            if (allocationUnit is null)
            {
                continue;
            }

            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            var segment = index.CompressedRowGroups
                               .First(r => r.RowGroupId == rowGroupId)
                               .Segments
                               .First(s => s.Key.ColumnId == columnId);

            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            var entries = blob.RleEntries.Where(e => !e.IsTerminator).ToList();

            var literals = entries.Where(e => !e.IsBitpacked).ToList();

            var packed = entries.Where(e => e.IsBitpacked).ToList();

            var width = blob.Header.BitpackEntrySize;

            var breakEven = width > 0 ? 64.0 / width : 0;

            lines.Add($"{tableName} rg{rowGroupId} col{columnId} rows {segment.RowCount} bits {width} "
                      + $"breakEven {breakEven:F2} entries {entries.Count}");

            lines.Add($"  literal runs {literals.Count} rows {literals.Sum(e => (long)e.Count)} "
                      + $"count min {(literals.Count > 0 ? literals.Min(e => e.Count) : 0)} "
                      + $"max {(literals.Count > 0 ? literals.Max(e => e.Count) : 0)} "
                      + $"mean {(literals.Count > 0 ? literals.Average(e => e.Count) : 0):F1}");

            lines.Add($"  packed runs {packed.Count} rows {packed.Sum(e => (long)e.Count)} "
                      + $"count min {(packed.Count > 0 ? packed.Min(e => e.Count) : 0)} "
                      + $"max {(packed.Count > 0 ? packed.Max(e => e.Count) : 0)} "
                      + $"mean {(packed.Count > 0 ? packed.Average(e => e.Count) : 0):F1}");

            var belowBreakEven = literals.Count(e => e.Count <= breakEven);

            lines.Add($"  literal runs at or below break even {belowBreakEven}");

            var perUnit = blob.Bitpack.ValuesPerUnit;

            lines.Add($"  units {blob.Header.BitpackUnitCount} perUnit {perUnit} "
                      + $"derivedValues {blob.Bitpack.ValuesPerUnit * blob.Header.BitpackUnitCount} packedRows {packed.Sum(e => (long)e.Count)}");

            if (perUnit > 0 && packed.Count > 0)
            {
                lines.Add($"  spans starting on a unit boundary {packed.Count(e => e.BitpackIndex % perUnit == 0)}"
                          + $"/{packed.Count} | counts that are a whole number of units "
                          + $"{packed.Count(e => e.Count % perUnit == 0)}/{packed.Count}");

                foreach (var entry in packed.Take(6))
                {
                    lines.Add($"    index {entry.BitpackIndex} count {entry.Count}");
                }
            }
        }

        foreach (var line in lines)
        {
            TestOutput.WriteLine(line);
        }

        File.WriteAllLines(Path.Combine("C:", "ColumnstoreDump", "rle_split_probe.txt"), lines);
    }
}
