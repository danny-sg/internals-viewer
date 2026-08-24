using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Sweeps the lab for the fields still unaccounted for, all of which need many segments rather than one
/// </summary>
public sealed class OpenQuestionsProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Open_Questions()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        var unknown0C = new SortedDictionary<string, int>();

        var wideTail = new SortedDictionary<string, int>();

        var pageFlags = new SortedDictionary<string, int>();

        var bookmarkFit = new SortedDictionary<string, int>();

        var examined = 0;

        foreach (var allocationUnit in database.AllocationUnits.Values.Where(a => a.TableName.StartsWith("Seg")))
        {
            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            foreach (var rowGroup in index.CompressedRowGroups)
            {
                foreach (var segment in rowGroup.Segments)
                {
                    if (examined >= 250)
                    {
                        break;
                    }

                    try
                    {
                        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                        examined++;

                        Count(unknown0C, $"0x{blob.Header.Unknown0C:X}");

                        // The four bytes past the count in a wide entry, which nothing reads
                        if (blob.Header.RleEntryBytes > InternalsViewer.Internals.Columnstore.Segments.SegmentBlob.EntrySize)
                        {
                            var span = blob.Data.Span;

                            for (var i = 0; i < blob.Header.RleEntryCount; i++)
                            {
                                var at = blob.Header.RleArrayOffset + (i * blob.Header.RleEntryBytes) + 12;

                                if (at + 4 <= span.Length)
                                {
                                    var entry = blob.RleEntries[i];

                                    Count(wideTail, $"{span[at]:X2} {span[at + 1]:X2} {span[at + 2]:X2} {span[at + 3]:X2}"
                                                    + $" on {(entry.IsTerminator ? "terminator" : entry.IsValue ? "repeat" : "read")}"
                                                    + $" value 0x{(ulong)entry.Value:X16} count {entry.Count}");
                                }
                            }
                        }

                        if (blob.VariableLengthData is { } store)
                        {
                            foreach (var page in store.Pages)
                            {
                                Count(pageFlags, $"flags {page.Flags:X} compression {page.Compression}");
                            }

                            // Does the declared count minus two land on the intervals the rows need
                            var needed = (segment.RowCount + blob.Header.BookmarkDistance - 1)
                                         / Math.Max(1, blob.Header.BookmarkDistance);

                            Count(bookmarkFit, $"declared-2 {blob.Bookmarks.Length} needed {needed} "
                                               + $"difference {blob.Bookmarks.Length - needed}");
                        }
                    }
                    catch (Exception exception)
                    {
                        _lines.Add($"{allocationUnit.TableName} col{segment.Key.ColumnId}: {exception.Message}");
                    }
                }
            }
        }

        _lines.Add($"examined {examined} segments");

        Report("+0x0C values", unknown0C);

        Report("wide RLE entry bytes 12-15", wideTail);

        Report("value page flags nibble", pageFlags);

        Report("VLD bookmark count against intervals needed", bookmarkFit);

        ProbeDump.Write("open_questions_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static void Count(SortedDictionary<string, int> into, string key)
    {
        into.TryGetValue(key, out var count);

        into[key] = count + 1;
    }

    private void Report(string name, SortedDictionary<string, int> values)
    {
        _lines.Add($"{name}: {(values.Count == 0 ? "none" : string.Join(", ", values.Take(10).Select(v => $"[{v.Key}] x{v.Value}")))}");
    }
}
