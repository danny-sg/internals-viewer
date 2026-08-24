using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// What a bookmark position points at, which is not the same thing for the two RLE types
/// </summary>
public sealed class BookmarkMeaningProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Bookmark_Positions()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var bitPack = new Totals();

        var variable = new Totals();

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    if (bitPack.Segments + variable.Segments >= 80)
                    {
                        break;
                    }

                    try
                    {
                        Examine(await service.GetSegmentBlob(database, segment, CancellationToken.None),
                                bitPack,
                                variable);
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");
                    }
                }
            }
        }

        _lines.Add($"BIT PACK  segments {bitPack.Segments} bookmarks {bitPack.Bookmarks} "
                   + $"positionHoldsRleEntry {bitPack.Agreed} sentinels {bitPack.Sentinels} "
                   + $"nonZeroHighBits {bitPack.HighBits}");

        _lines.Add($"VLD       segments {variable.Segments} bookmarks {variable.Bookmarks} "
                   + $"positionHoldsRleEntry {variable.Agreed} sentinels {variable.Sentinels} "
                   + $"nonZeroHighBits {variable.HighBits} tailIsRleArray {variable.TailIsRle}/{variable.Segments}");

        _lines.AddRange(variable.Lines);

        ProbeDump.Write("bookmark_meaning_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static void Examine(SegmentBlob blob, Totals bitPack, Totals variable)
    {
        if (blob.Bookmarks.Length == 0 || blob.Header.BookmarkDistance <= 0)
        {
            return;
        }

        var totals = blob.Header.IsVariableLengthData ? variable : bitPack;

        totals.Segments++;

        // The RLE array starts sixteen bytes before the declared bookmark array ends, so the tail may be RLE data
        if (blob.Header.IsVariableLengthData && blob.Bookmarks.Length >= 2 && blob.RleEntries.Length >= 2)
        {
            var tail = blob.Bookmarks[^2..];

            var matches = tail[0].Position == (int)blob.RleEntries[0].Value
                          && tail[0].EndRow == blob.RleEntries[0].Count
                          && tail[1].Position == (int)blob.RleEntries[1].Value
                          && tail[1].EndRow == blob.RleEntries[1].Count;

            if (matches)
            {
                totals.TailIsRle++;
            }
            else if (totals.Lines.Count < 6)
            {
                totals.Lines.Add($"  TAIL MISMATCH last two [{tail[0].Position:X8}/{tail[0].EndRow}] "
                                 + $"[{tail[1].Position:X8}/{tail[1].EndRow}] versus rle "
                                 + $"[{(uint)blob.RleEntries[0].Value:X8}/{blob.RleEntries[0].Count}] "
                                 + $"[{(uint)blob.RleEntries[1].Value:X8}/{blob.RleEntries[1].Count}]");
            }
        }

        var stream = new SegmentDataIdStream(blob);

        for (var i = 0; i < blob.Bookmarks.Length; i++)
        {
            var bookmark = blob.Bookmarks[i];

            totals.Bookmarks++;

            if (bookmark.Position == unchecked((int)0x80000000))
            {
                totals.Sentinels++;

                continue;
            }

            // The high bits are where a page and slot pair would keep its slot, so a page alone leaves them clear
            if ((bookmark.Position & ~0x7FFF) != 0)
            {
                totals.HighBits++;
            }

            var row = Math.Min(Math.Max(0, bookmark.EndRow - 1), stream.RowCount - 1);

            var entryIndex = bookmark.GetRleEntryIndex(blob.Header.RleEntryBytes);

            var start = 0;

            for (var e = 0; e < entryIndex && e < blob.RleEntries.Length; e++)
            {
                start += blob.RleEntries[e].Count;
            }

            if (entryIndex < blob.RleEntries.Length
                && row >= start
                && row < start + blob.RleEntries[entryIndex].Count)
            {
                totals.Agreed++;
            }
        }
    }

    private sealed class Totals
    {
        public int Segments { get; set; }

        public int Bookmarks { get; set; }

        public int Agreed { get; set; }

        public int Sentinels { get; set; }

        public int HighBits { get; set; }

        public int Dumped { get; set; }

        public int TailIsRle { get; set; }

        public List<string> Lines { get; } = [];
    }
}
