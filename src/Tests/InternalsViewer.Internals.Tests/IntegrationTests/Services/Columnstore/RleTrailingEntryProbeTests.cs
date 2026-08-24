using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether the RLE array holds entries past its terminator, the declared count being units rather than entries
/// </summary>
public sealed class RleTrailingEntryProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Entries_Past_The_Terminator()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var trailing = 0;

        var mismatched = 0;

        var examined = 0;

        var widthMisses = 0;

        var wideSegments = 0;

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    if (examined >= 400)
                    {
                        break;
                    }

                    SegmentReport report;

                    try
                    {
                        report = Examine(await service.GetSegmentBlob(database, segment, CancellationToken.None),
                                         segment.RowCount);
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");

                        continue;
                    }

                    examined++;

                    var blobForWidth = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                    var actualWide = blobForWidth.RleEntries.Length * 2 == blobForWidth.Header.RleArrayCount
                                     && blobForWidth.Header.RleArrayCount > 0;

                    // A dictionary segment stores slot numbers, so its base and magnitude are unset and unusable
                    var scaled = segment.BaseId >= 0 && segment.Magnitude > 0;

                    var storedMax = scaled ? (segment.MaxDataId / segment.Magnitude) - segment.BaseId : 0;

                    var predictedWide = scaled && storedMax > int.MaxValue;

                    if (actualWide != predictedWide)
                    {
                        _lines.Add($"WIDTH {allocationUnit.TableName} col{segment.Key.ColumnId} "
                                   + $"enc {(int)segment.Encoding} actualWide {actualWide} predicted {predictedWide} "
                                   + $"base {segment.BaseId} magnitude {segment.Magnitude} "
                                   + $"max {segment.MaxDataId} stored {storedMax}");

                        widthMisses++;
                    }
                    else if (actualWide)
                    {
                        wideSegments++;
                    }

                    if ((allocationUnit.TableName == "SegHugeDict" && segment.Key.ColumnId == 4)
                        || (allocationUnit.TableName == "SegTypes" && segment.Key.ColumnId == 10)
                        || (allocationUnit.TableName == "SegEdgeTypes" && segment.Key.ColumnId == 6))
                    {
                        var header = (await service.GetSegmentBlob(database, segment, CancellationToken.None)).Header;

                        _lines.Add($"HEADER {allocationUnit.TableName} col{segment.Key.ColumnId} "
                                   + $"enc {(int)segment.Encoding} version {header.Version} lobType {(int)header.LobType} "
                                   + $"reserved {header.Reserved} unknown0C 0x{header.Unknown0C:X} "
                                   + $"structure {(int)header.RleType} bookmarks {header.BookmarkCount} "
                                   + $"distance {header.BookmarkDistance} rleArrayCount {header.RleArrayCount} "
                                   + $"rleEntrySize {header.RleEntrySize} bitpackEntrySize {header.BitpackEntrySize} "
                                   + $"bitpackUnits {header.BitpackUnitCount} minId {header.BitpackMinId} "
                                   + $"size {segment.OnDiskSize} rows {segment.RowCount} "
                                   + $"minData {segment.MinDataId} maxData {segment.MaxDataId}");
                    }

                    if (report.Trailing > 0)
                    {
                        trailing++;

                        if (trailing <= 8)
                        {
                            _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId} rg{rowGroup.RowGroupId} "
                                       + $"structure {report.Structure} declared {report.Declared} used {report.Used} "
                                       + $"trailing {report.Trailing} [{report.TrailingText}]");
                        }
                    }

                    if (report.SummedRows != report.ActualRows)
                    {
                        mismatched++;

                        if (mismatched <= 14)
                        {
                            _lines.Add($"ROWCOUNT {allocationUnit.TableName} col{segment.Key.ColumnId} "
                                       + $"encoding {(int)segment.Encoding} structure {report.Structure} "
                                       + $"bitpackEntrySize {report.BitpackEntrySize} entryBytes {report.EntryBytes} "
                                       + $"declared {report.Declared} summed {report.SummedRows} "
                                       + $"actual {report.ActualRows}");
                        }
                    }
                }
            }
        }

        _lines.Add($"WIDTH PREDICTION: {widthMisses} disagreements, {wideSegments} wide segments predicted correctly");

        _lines.Add($"examined {examined} segments, {trailing} with entries past the terminator, "
                   + $"{mismatched} whose summed rows differ from the segment row count");

        ProbeDump.Write("rle_trailing_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static SegmentReport Examine(InternalsViewer.Internals.Columnstore.Segments.SegmentBlob blob, int actualRows)
    {
        var entries = blob.RleEntries;

        var terminator = -1;

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].IsTerminator)
            {
                terminator = i;

                break;
            }
        }

        var used = terminator >= 0 ? terminator + 1 : entries.Length;

        var text = string.Join(" | ", entries.Skip(used).Take(4)
                                             .Select(e => $"0x{(uint)e.Value:X8} x{e.Count}"));

        return new SegmentReport((int)blob.Header.RleType,
                                 blob.Header.BitpackEntrySize,
                                 blob.Header.RleEntryBytes,
                                 blob.Header.RleEntryCount,
                                 used,
                                 entries.Length - used,
                                 text,
                                 entries.Sum(e => e.Count),
                                 actualRows);
    }

    private readonly record struct SegmentReport(int Structure,
                                                 int BitpackEntrySize,
                                                 int EntryBytes,
                                                 int Declared,
                                                 int Used,
                                                 int Trailing,
                                                 string TrailingText,
                                                 int SummedRows,
                                                 int ActualRows);
}
