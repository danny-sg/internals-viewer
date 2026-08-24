using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class WideNullProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Wide_Nulls_And_Archive()
    {
        await BuildTables();

        var database = await LoadDatabase();

        var service = CreateService();

        await Report(service, database, "SegWideNull");

        await Report(service, database, "SegArchiveWide");

        await Report(service, database, "SegExact");

        ProbeDump.Write("wide_null_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private async Task Report(ColumnstoreService service, DatabaseSource database, string tableName)
    {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == tableName);

        if (allocationUnit is null)
        {
            _lines.Add($"=== {tableName}: not found ===");

            return;
        }

        _lines.Add($"=== {tableName} ===");

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var rowGroup = index.CompressedRowGroups.FirstOrDefault();

        if (rowGroup is null)
        {
            _lines.Add("  no compressed row groups");

            return;
        }

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        var idSegment = rowGroup.Segments.FirstOrDefault(s => s.Key.ColumnId == 2);

        var idReader = idSegment is null ? null : await service.GetSegmentReader(database, idSegment, CancellationToken.None);

        foreach (var segment in rowGroup.Segments.Where(s => s.Key.ColumnId > 2))
        {
            var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

            try
            {
                var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                if (blob.VariableLengthData is { } store)
                {
                    _lines.Add($"    store valueCount {store.ValueCount} pages {store.PageCount} "
                               + $"sizes [{string.Join(",", store.PageSizes.Take(6))}] pages ["
                               + string.Join(" | ", store.Pages.Take(4).Select(
                                   x => $"sub {(int)x.SubLobType} comp {x.Compression} flags {x.Flags} "
                                        + $"size {x.ValueSize} count {x.ValueCount} bytes {x.Size}")) + "]");
                }

                var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

                var nulls = 0;

                var matched = 0;

                var checkedRows = 0;

                var mismatch = string.Empty;

                // Sampled across the whole segment, the runs meaning the first rows are not representative
                var sample = new List<int> { 0, 1, 2, 500, 4999, 5000, 5001, 6000, 9000, 12000, 15000, 17000, 19999 };

                foreach (var i in sample.Where(x => x < segment.RowCount))
                {
                    var decoded = reader.GetValue(i);

                    if (decoded is null)
                    {
                        nulls++;
                    }

                    if (idReader is null)
                    {
                        continue;
                    }

                    await using var command = new SqlCommand(
                        $"SELECT LEFT(CONVERT(varchar(120), {column?.Name}, 121), 60) FROM {tableName} WHERE Id = @id", connection);

                    command.Parameters.AddWithValue("@id", idReader.GetValue(i) ?? (object)DBNull.Value);

                    var actual = await command.ExecuteScalarAsync();

                    var expected = actual is null or DBNull ? null : actual.ToString();

                    var got = decoded switch
                    {
                        null => null,
                        byte[] bytes => "0x" + Convert.ToHexString(bytes),
                        _ => decoded.ToString()
                    };

                    checkedRows++;

                    if (got is { Length: > 60 })
                    {
                        got = got[..60];
                    }

                    if (string.Equals(got, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        matched++;
                    }
                    else if (mismatch.Length == 0)
                    {
                        mismatch = $" FIRST MISMATCH row {i} decoded '{got ?? "<null>"}' actual '{expected ?? "<null>"}'";
                    }
                }

                _lines.Add($"col{segment.Key.ColumnId} {column?.Name} enc {(int)segment.Encoding} "
                           + $"structure {(int)blob.Header.StructureType} hasNulls {segment.HasNulls} "
                           + $"nullValue {segment.NullValue} | decoded nulls {nulls}/200 "
                           + $"| null agreement {matched}/{checkedRows}{mismatch}");
            }
            catch (Exception exception)
            {
                _lines.Add($"col{segment.Key.ColumnId} {column?.Name}: {exception.Message} << {string.Join(" << ", (exception.StackTrace ?? string.Empty).Split(Environment.NewLine).Take(3).Select(x => x.Trim()))}");

                try
                {
                    var lob = new LobDataService(ServiceHelper.CreatePageService(TestOutput));

                    var bytes = await lob.GetData(database,
                                                  new InternalsViewer.Internals.Engine.Address.RowIdentifier(
                                                      segment.DataPointer.PageAddress, (ushort)segment.DataPointer.Slot),
                                                  CancellationToken.None);

                    var raw = new ReadOnlyMemory<byte>(bytes);

                    if (InternalsViewer.Internals.Columnstore.Segments.ArchiveBlobHeader.IsArchive(raw.Span))
                    {
                        var at = 4;

                        var blocks = new List<string>();

                        while (at + 8 <= raw.Length && blocks.Count < 8)
                        {
                            var un = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(raw.Span.Slice(at, 4));

                            var comp = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(raw.Span.Slice(at + 4, 4));

                            blocks.Add($"un {un} comp {comp}{(un == comp ? " RAW" : string.Empty)}");

                            at += 8 + comp;
                        }

                        _lines.Add($"    archive blocks [{string.Join(" | ", blocks)}]");

                        raw = InternalsViewer.Internals.Columnstore.Parsers.ArchiveBlobExpander.Expand(raw);
                    }

                    _lines.Add($"    prologue {Convert.ToHexString(raw.Span[..Math.Min(64, raw.Length)])}");

                    _lines.Add($"    length {raw.Length} onDisk {segment.OnDiskSize}");

                    var header = InternalsViewer.Internals.Columnstore.Parsers.SegmentBlobParser.ParseHeader(raw.Span);

                    var expected = header.VariableLengthDataOffset;

                    _lines.Add($"    bookmarks {header.BookmarkCount} rleArrayCount {header.RleArrayCount} "
                               + $"expected store at {expected} bytes there "
                               + $"{Convert.ToHexString(raw.Span.Slice(expected, 24))}");

                    // The store opens with a sub lob type of eight, so the first one of those is where it really is
                    for (var probe = expected - 64; probe < expected + 256; probe += 4)
                    {
                        if (probe < 0 || probe + 24 > raw.Length)
                        {
                            continue;
                        }

                        if (System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(raw.Span.Slice(probe, 4)) != 8)
                        {
                            continue;
                        }

                        _lines.Add($"    sub lob type 8 found at {probe} (expected {expected}, delta {probe - expected}) "
                                   + $"{Convert.ToHexString(raw.Span.Slice(probe, 24))}");

                        break;
                    }
                }
                catch (Exception inner)
                {
                    _lines.Add($"    raw: {inner.Message}");
                }
            }
        }
    }

    private ColumnstoreService CreateService()
    {
        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var reader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                      pageService,
                                      new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput)),
                                      new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        return new ColumnstoreService(reader, new LobDataService(pageService));
    }

    private async Task BuildTables()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        // Nulls in a store by value column, which has no data id for the null sentinel to be
        await Build(connection, "SegWideNull", """
            CREATE TABLE SegWideNull (Id int NOT NULL, Guid1 uniqueidentifier NULL, Bin20 binary(20) NULL,
                Dto datetimeoffset(7) NULL, Text1 varchar(max) NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegWideNull ON SegWideNull;
            INSERT INTO SegWideNull WITH (TABLOCK)
            SELECT TOP (20000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 3 = 0 THEN NULL ELSE NEWID() END,
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 4 = 0 THEN NULL
                        ELSE CAST(NEWID() AS binary(20)) END,
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 5 = 0 THEN NULL
                        ELSE TODATETIMEOFFSET(DATEADD(second, ABS(CHECKSUM(NEWID())) % 100000000, '2000-01-01'), 330) END,
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 7 = 0 THEN NULL
                        ELSE REPLICATE(CAST('w' AS varchar(max)), 9000) END
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        // Archive compression wrapped around a store that holds a raw page, two envelopes at once
        await Build(connection, "SegArchiveWide", """
            CREATE TABLE SegArchiveWide (Id int NOT NULL, Guid1 uniqueidentifier NOT NULL, Bin20 binary(20) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegArchiveWide ON SegArchiveWide
                WITH (DATA_COMPRESSION = COLUMNSTORE_ARCHIVE);
            INSERT INTO SegArchiveWide WITH (TABLOCK)
            SELECT TOP (20000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   NEWID(), CAST(NEWID() AS binary(20))
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        // Lengths either side of eight thousand, to pin the pointer threshold to the byte
        await Build(connection, "SegExact", """
            CREATE TABLE SegExact (Id int NOT NULL, Text1 varchar(max) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegExact ON SegExact;
            INSERT INTO SegExact WITH (TABLOCK)
            SELECT TOP (2000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   REPLICATE(CAST('e' AS varchar(max)), 7996 + (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 10))
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        foreach (var tableName in new[] { "SegWideNull", "SegArchiveWide", "SegExact" })
        {
            await using var command = new SqlCommand(
                $"ALTER INDEX ALL ON {tableName} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
            {
                CommandTimeout = 900
            };

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task Build(SqlConnection connection, string tableName, string script)
    {
        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{tableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 900 };

            await command.ExecuteNonQueryAsync();
        }
    }
}
