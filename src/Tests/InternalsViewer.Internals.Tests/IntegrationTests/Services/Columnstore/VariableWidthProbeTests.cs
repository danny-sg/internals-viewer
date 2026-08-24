using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class VariableWidthProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Variable_Width_Pages()
    {
        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(reader, new LobDataService(pageService));

        foreach (var (tableName, columnId) in new[] { ("SegWideNull", 4), ("SegWideNull", 3), ("SegWideNull", 5) })
        {
            var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == tableName);

            var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

            var segment = index.CompressedRowGroups.First().Segments.First(s => s.Key.ColumnId == columnId);

            var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == columnId);

            var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

            if (blob.VariableLengthData is not { } store)
            {
                continue;
            }

            _lines.Add($"=== {tableName} col{columnId} {column?.Name} rows {segment.RowCount} "
                       + $"storeValueCount {store.ValueCount} pages {store.PageCount} ===");

            for (var i = 0; i < store.Pages.Length; i++)
            {
                var page = store.Pages[i];

                var raw = blob.Data.Span.Slice(page.Offset, Math.Min(40, page.Size));

                _lines.Add($"  page {i} offset {page.Offset} size {page.Size} comp {page.Compression} "
                           + $"flags {page.Flags} valueSize {page.ValueSize} valueCount {page.ValueCount} "
                           + $"payloadSize {page.PayloadSize} | {Convert.ToHexString(raw)}");
            }

            foreach (var page in store.Pages.Where(x => x.IsVariableWidth).Take(1))
            {
                var expanded = page.PayloadSize + 1;

                try
                {
                    var decoder = new InternalsViewer.Internals.Compression.XpressHuffmanDecoder();

                    var payload = blob.Data.Slice(page.Offset + 14, page.Size - 14);

                    var values = decoder.Decode(payload, expanded);

                    var span = values.Span;

                    _lines.Add($"  DECODED {values.Length} bytes with size {expanded}");

                    _lines.Add($"    head64 {Convert.ToHexString(span[..64])}");

                    _lines.Add($"    tail32 {Convert.ToHexString(span[^32..])}");

                    var heads = new List<string>();

                    for (var i = 0; i < 8; i++)
                    {
                        heads.Add($"{System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i * 2, 2))}");
                    }

                    _lines.Add($"    first u16s [{string.Join(",", heads)}]");

                    var ascending = 0;

                    var previous = -1;

                    for (var i = 0; i < page.ValueCount && (i * 2) + 2 <= span.Length; i++)
                    {
                        var current = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i * 2, 2));

                        if (current >= previous)
                        {
                            ascending++;
                        }

                        previous = current;
                    }

                    _lines.Add($"    u16 entries ascending {ascending}/{page.ValueCount} "
                               + $"last {System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice((page.ValueCount - 1) * 2, 2))}");

                    var arrayStart = values.Length - (page.ValueCount * 2);

                    var reverse = new List<string>();

                    for (var i = 0; i < 10; i++)
                    {
                        var at = values.Length - ((i + 1) * 2);

                        reverse.Add($"{System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(at, 2))}");
                    }

                    var maxOffset = 0;

                    for (var i = 0; i < page.ValueCount; i++)
                    {
                        var at = values.Length - ((i + 1) * 2);

                        var offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(at, 2));

                        if (offset != 0xFEFF && offset > maxOffset)
                        {
                            maxOffset = offset;
                        }
                    }

                    _lines.Add($"    arrayStart {arrayStart} reverse[0..9] [{string.Join(",", reverse)}] "
                               + $"maxOffset {maxOffset}");

                    var entries = new List<ushort>();

                    for (var i = 0; i < page.ValueCount; i++)
                    {
                        entries.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i * 2, 2)));
                    }

                    var grouped = entries.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(3).ToList();

                    _lines.Add($"    entries distinct {entries.Distinct().Count()} of {entries.Count} "
                               + $"| most common {string.Join(", ", grouped.Select(g => $"{g.Key} x{g.Count()}"))} "
                               + $"| min {entries.Min()} max {entries.Max()}");

                    var multiples = entries.Count(x => x % 16 == 0);

                    var multiples20 = entries.Count(x => x % 20 == 0);

                    var multiples10 = entries.Count(x => x % 10 == 0);

                    _lines.Add($"    multiples of 16 {multiples} of 20 {multiples20} of 10 {multiples10}");

                    var arrayBytes = page.ValueCount * 2;

                    _lines.Add($"    bytes after the array {values.Length - arrayBytes} "
                               + $"head {Convert.ToHexString(span.Slice(arrayBytes, 48))}");
                }
                catch (Exception exception)
                {
                    _lines.Add($"  DECODE with {expanded} failed: {exception.Message}");
                }
            }

            // The trailing units a nullable segment writes between the bookmarks and the store
            var trailing = blob.Header.TrailingRleUnits;

            if (trailing > 0)
            {
                var at = blob.Header.BookmarkArrayOffset + (blob.Header.BookmarkCount * 8);

                _lines.Add($"  trailing units {trailing} at {at} "
                           + $"{Convert.ToHexString(blob.Data.Span.Slice(at, trailing * 8))}");
            }

            _lines.Add($"  bookmark[0..3] {Convert.ToHexString(blob.Data.Span.Slice(blob.Header.BookmarkArrayOffset, 24))}");
        }

        File.WriteAllLines(Path.Combine("C:", "ColumnstoreDump", "variable_width_probe.txt"), _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
