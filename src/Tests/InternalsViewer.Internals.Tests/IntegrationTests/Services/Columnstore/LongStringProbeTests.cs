using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class LongStringProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegLongString";

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Long_String_Storage()
    {
        var lines = new List<string>();

        await using (var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local")))
        {
            await connection.OpenAsync();

            await using (var command = new SqlCommand($"""
                                                       SELECT TOP (3) Id, LEN(Repeated), DATALENGTH(Repeated),
                                                              LEN(Distinct1), DATALENGTH(Distinct1)
                                                       FROM {TableName} ORDER BY Id
                                                       """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    lines.Add($"row {reader.GetValue(0)} repeated len {reader.GetValue(1)} bytes {reader.GetValue(2)} "
                              + $"| distinct len {reader.GetValue(3)} bytes {reader.GetValue(4)}");
                }
            }

            await using (var command = new SqlCommand($"""
                                                       SELECT au.type_desc, au.total_pages, au.used_pages, au.data_pages
                                                       FROM sys.allocation_units au
                                                       JOIN sys.partitions p ON p.hobt_id = au.container_id
                                                       WHERE p.object_id = OBJECT_ID('{TableName}')
                                                       """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    lines.Add($"allocation unit {reader.GetValue(0)} total {reader.GetValue(1)} "
                              + $"used {reader.GetValue(2)} data {reader.GetValue(3)}");
                }
            }

            await using (var command = new SqlCommand($"""
                                                       SELECT s.column_id, s.encoding_type, s.on_disk_size, s.row_count,
                                                              s.min_data_id, s.max_data_id,
                                                              DATALENGTH(s.min_deep_data), DATALENGTH(s.max_deep_data)
                                                       FROM sys.column_store_segments s
                                                       JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                       WHERE p.object_id = OBJECT_ID('{TableName}')
                                                       """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    lines.Add($"segment col {reader.GetValue(0)} enc {reader.GetValue(1)} size {reader.GetValue(2)} "
                              + $"rows {reader.GetValue(3)} minId {reader.GetValue(4)} maxId {reader.GetValue(5)} "
                              + $"minDeep {reader.GetValue(6)} maxDeep {reader.GetValue(7)}");
                }
            }
        }

        var database = await LoadDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var recordReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                            pageService,
                                            new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                            new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new ColumnstoreService(recordReader, new LobDataService(pageService));

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == TableName);

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        foreach (var segment in index.CompressedRowGroups.First().Segments.Where(s => s.Key.ColumnId > 2))
        {
            var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

            for (var i = 0; i < 2; i++)
            {
                var value = reader.GetValue(i);

                var text = value?.ToString() ?? string.Empty;

                lines.Add($"col{segment.Key.ColumnId} row {i} dataId {reader.DataIds.GetRowDataId(i)} "
                          + $"decoded length {text.Length} starts '{text[..Math.Min(24, text.Length)]}'");
            }

            var metadata = segment.LocalDictionary
                           ?? index.Columns.First(c => c.ColumnStoreColumnId == segment.Key.ColumnId).GlobalDictionary;

            if (metadata is null)
            {
                continue;
            }

            var blob = await service.GetDictionaryBlob(database, metadata, CancellationToken.None);

            if (blob is not StringDictionary strings)
            {
                continue;
            }

            lines.Add($"col{segment.Key.ColumnId} dictionary entries {strings.EntryCount} "
                      + $"pages {strings.Pages.Length} maxString {strings.Store.MaxStringSize} "
                      + $"stringCount {strings.Store.StringCount}");

            for (var i = 0; i < Math.Min(2, strings.EntryCount); i++)
            {
                var bytes = strings.GetValueBytes(strings.FirstId + i);

                lines.Add($"  entry {i} bytes {bytes.Length} hex {Convert.ToHexString(bytes[..Math.Min(24, bytes.Length)])}");

                if (bytes.Length < 22)
                {
                    continue;
                }

                var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2, 4));

                var blobId = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(6, 8));

                var pageId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14, 4));

                var fileId = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18, 2));

                var slot = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(20, 2));

                lines.Add($"    reads as length {length} blobId {blobId} page ({fileId}:{pageId}) slot {slot}");

                try
                {
                    var page = await pageService.GetPage(database,
                                                         new InternalsViewer.Internals.Engine.Address.PageAddress((short)fileId, pageId),
                                                         CancellationToken.None);

                    lines.Add($"    page ({fileId}:{pageId}) type {page.PageHeader.PageType} "
                              + $"allocationUnit {page.PageHeader.AllocationUnitId}");
                }
                catch (Exception exception)
                {
                    lines.Add($"    page ({fileId}:{pageId}): {exception.Message}");
                }

                try
                {
                    var lob = new LobDataService(pageService);

                    var payload = await lob.GetData(database,
                                                    new InternalsViewer.Internals.Engine.Address.RowIdentifier(
                                                        new InternalsViewer.Internals.Engine.Address.PageAddress((short)fileId, pageId),
                                                        (ushort)slot),
                                                    CancellationToken.None);

                    var distinct = payload.Distinct().Take(6).Select(b => $"{b:X2}");

                    lines.Add($"    payload {payload.Length} bytes, expected {length}, "
                              + $"distinct bytes [{string.Join(" ", distinct)}] "
                              + $"head {Convert.ToHexString(payload.AsSpan(0, Math.Min(24, payload.Length)))}");

                    var memory = new ReadOnlyMemory<byte>(payload);

                    lines.Add($"    isArchive {InternalsViewer.Internals.Columnstore.Segments.ArchiveBlobHeader.IsArchive(memory.Span)}");

                    var expanded = InternalsViewer.Internals.Columnstore.Parsers.ArchiveBlobExpander.Expand(memory);

                    var text = System.Text.Encoding.ASCII.GetString(expanded.Span);

                    lines.Add($"    EXPANDED {expanded.Length} bytes, head '{text[..Math.Min(30, text.Length)]}' "
                              + $"tail '{text[Math.Max(0, text.Length - 30)..]}' "
                              + $"allSame {text.All(c => c == text[0])}");
                }
                catch (Exception exception)
                {
                    lines.Add($"    payload: {exception.Message}");
                }
            }
        }

        ProbeDump.Write("long_string_probe.txt", lines);

        foreach (var line in lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
