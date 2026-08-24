using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class EdgeCaseProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Edge_Cases()
    {
        await BuildTables();

        var database = await LoadDatabase();

        var service = CreateService();

        foreach (var tableName in new[] { "SegEdgeTypes", "SegLadder", "SegAllNull" })
        {
            await Report(service, database, tableName);
        }

        await Verify(service, database);

        ProbeDump.Write("edge_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    /// <summary>
    /// Reads each wide column back against the table, a value that decodes still having to be the right one
    /// </summary>
    private async Task Verify(ColumnstoreService service, DatabaseSource database)
    {
        var allocationUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == "SegEdgeTypes");

        if (allocationUnit is null)
        {
            return;
        }

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        // The row group was filled by one bulk insert, so its ordinals follow the Id the rows were numbered with
        var idReader = await service.GetSegmentReader(database,
                                                      index.CompressedRowGroups.First().Segments.First(x => x.Key.ColumnId == 2),
                                                      CancellationToken.None);

        foreach (var (columnId, columnName) in new[] { (4, "Dto"), (5, "Dt2") })
        {
            var segment = index.CompressedRowGroups.First().Segments.First(x => x.Key.ColumnId == columnId);

            var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

            for (var i = 0; i < 3; i++)
            {
                var id = idReader.GetValue(i);

                var decoded = reader.GetValue(i);

                await using var command = new SqlCommand(
                    $"SELECT CONVERT(varchar(60), {columnName}, 121) FROM SegEdgeTypes WHERE Id = @id", connection);

                command.Parameters.AddWithValue("@id", id ?? (object)DBNull.Value);

                var actual = await command.ExecuteScalarAsync();

                _lines.Add($"  ROW {columnName} id {id} decoded '{decoded}' actual '{actual}'");
            }
        }

        foreach (var (columnId, columnName) in new[] { (3, "Guid1"), (4, "Dto"), (5, "Dt2"), (6, "T1"),
                                                       (7, "Dec38"), (8, "Bin20"), (9, "NChar10") })
        {
            var segment = index.CompressedRowGroups.First().Segments.First(x => x.Key.ColumnId == columnId);

            var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

            var matched = 0;

            for (var i = 0; i < 10; i++)
            {
                var value = reader.GetValue(i);

                // A value that decodes to text has to be compared as text, the byte form being the string's own
                var predicate = value is string
                    ? $"CONVERT(varchar(60), {columnName}, 121) = @value"
                    : $"CAST({columnName} AS varbinary(50)) = CAST(@value AS varbinary(50))";

                if (i == 0)
                {
                    _lines.Add($"  {columnName} row 0 decodes to {value?.GetType().Name} '{value}'");
                }

                await using var command = new SqlCommand(
                    $"SELECT COUNT(*) FROM SegEdgeTypes WHERE {predicate}", connection);

                command.Parameters.AddWithValue("@value", value ?? (object)DBNull.Value);

                if (await command.ExecuteScalarAsync() is int count && count > 0)
                {
                    matched++;
                }
            }

            _lines.Add($"VERIFY {columnName}: {matched}/10 decoded values found in the table");
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
            _lines.Add($"  no compressed row groups, states [{string.Join(",", index.RowGroups.Select(r => r.State))}]");

            return;
        }

        foreach (var segment in rowGroup.Segments)
        {
            var column = index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == segment.Key.ColumnId);

            var type = column?.Structure is { } s ? $"{s.DataType}({s.Precision},{s.Scale},{s.DataLength})" : "?";

            var structure = "-";

            var pageTypes = "-";

            try
            {
                var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

                structure = $"{(int)blob.Header.RleType}";

                pageTypes = blob.VariableLengthData is { } store
                    ? string.Join(",", store.Pages.Select(p => (int)p.SubLobType).Distinct())
                    : "-";

                if (blob.VariableLengthData is { } vld)
                {
                    var page = vld.Pages[0];

                    pageTypes += $" payloadTitles [{string.Join(",", vld.Pages.Select(x => x.Payload).Distinct())}] compression [{string.Join(",", vld.Pages.Select(x => x.Compression))}]"
                                 + $" store max {vld.MaxStringSize} elementSize {vld.ElementSize} pages {vld.PageCount}"
                                 + $" | page0 compression {page.Compression} flags {page.Flags} "
                                 + $"valueSize {page.ValueSize} valueCount {page.ValueCount} "
                                 + $"payloadSize {page.PayloadSize} size {page.Size}";
                }
            }
            catch (Exception exception)
            {
                structure = exception.Message;
            }

            if (structure == "7")
            {
                try
                {
                    var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

                    var value = reader.GetValue(0);

                    pageTypes += $" decodes to {value?.GetType().Name} '{value}'";
                }
                catch (Exception exception)
                {
                    pageTypes += $" DECODE FAILS: {exception.Message}";
                }
            }

            var metadata = segment.LocalDictionary ?? column?.GlobalDictionary;

            var dictionary = "none";

            if (metadata is not null)
            {
                try
                {
                    var blob = await service.GetDictionaryBlob(database, metadata, CancellationToken.None);

                    dictionary = $"type {metadata.Type} entries {blob.EntryCount}";

                    if (blob is StringDictionary strings)
                    {
                        var lengths = new List<int>();

                        var tags = new HashSet<string>();

                        var pointerLengths = new List<int>();

                        var inlineLengths = new List<int>();

                        // Sampled across the whole range rather than the head, the ladder rising with the entry id
                        var step = Math.Max(1, strings.EntryCount / 200);

                        for (var i = 0; i < strings.EntryCount; i += step)
                        {
                            var bytes = strings.GetValueBytes(strings.FirstId + i);

                            lengths.Add(bytes.Length);

                            if (bytes.Length == 22 && bytes[0] == 0x11 && bytes[1] == 0x01)
                            {
                                tags.Add($"{bytes[0]:X2}{bytes[1]:X2}");

                                pointerLengths.Add(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2, 4)));
                            }
                            else
                            {
                                inlineLengths.Add(bytes.Length);
                            }
                        }

                        dictionary += $" sampled {lengths.Count}"
                                      + $" inline {inlineLengths.Count} max {(inlineLengths.Count > 0 ? inlineLengths.Max() : 0)}"
                                      + $" | pointers {pointerLengths.Count} "
                                      + $"minValue {(pointerLengths.Count > 0 ? pointerLengths.Min() : 0)} "
                                      + $"tags [{string.Join(",", tags)}]"
                                      + $" pages {string.Join(",", strings.Pages.Select(p => (int)p.SubLobType).Distinct())}";
                    }
                }
                catch (Exception exception)
                {
                    dictionary = $"{exception.GetType().Name}: {exception.Message} | "
                                 + $"{string.Join(" << ", (exception.StackTrace ?? string.Empty).Split(Environment.NewLine).Take(4).Select(f => f.Trim()))} "
                                 + $"| pointer {metadata.DataPointer.PageAddress}:{metadata.DataPointer.Slot} "
                                 + $"entries {metadata.EntryCount} size {metadata.OnDiskSize}";
                }
            }

            _lines.Add($"col{segment.Key.ColumnId} {column?.Name} {type} enc {(int)segment.Encoding} "
                       + $"structure {structure} rows {segment.RowCount} nulls {segment.HasNulls} "
                       + $"nullValue {segment.NullValue} vldPages {pageTypes} | {dictionary}");
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

        // Types wider than the eight bytes the numeric encodings can hold, none of which the lab has ever had
        await Build(connection, "SegEdgeTypes", """
            CREATE TABLE SegEdgeTypes (Id int NOT NULL, Guid1 uniqueidentifier NOT NULL,
                Dto datetimeoffset(7) NOT NULL, Dt2 datetime2(7) NOT NULL, T1 time(7) NOT NULL,
                Dec38 decimal(38,10) NOT NULL, Bin20 binary(20) NOT NULL, NChar10 nchar(10) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegEdgeTypes ON SegEdgeTypes;
            INSERT INTO SegEdgeTypes WITH (TABLOCK)
            SELECT TOP (20000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   NEWID(),
                   TODATETIMEOFFSET(DATEADD(second, ABS(CHECKSUM(NEWID())) % 100000000, '2000-01-01'), 330),
                   DATEADD(second, ABS(CHECKSUM(NEWID())) % 100000000, CAST('2000-01-01' AS datetime2(7))),
                   CAST(DATEADD(millisecond, ABS(CHECKSUM(NEWID())) % 86400000, CAST('00:00' AS time(7))) AS time(7)),
                   CAST(ABS(CHECKSUM(NEWID())) AS decimal(38,10)) / 7,
                   CAST(NEWID() AS binary(20)),
                   CAST(ABS(CHECKSUM(NEWID())) % 100000 AS nchar(10))
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        // A ladder of lengths around the 8192 byte MaxStringSize, to find where the LOB pointer takes over
        await Build(connection, "SegLadder", """
            CREATE TABLE SegLadder (Id int NOT NULL, Text1 varchar(max) NOT NULL, NText nvarchar(max) NOT NULL,
                Bin varbinary(max) NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegLadder ON SegLadder;
            INSERT INTO SegLadder WITH (TABLOCK)
            SELECT TOP (4096) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   REPLICATE(CAST('x' AS varchar(max)), 4000 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
                       + CAST(NEWID() AS varchar(40)),
                   REPLICATE(CAST(N'y' AS nvarchar(max)), 2000 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
                       + CAST(NEWID() AS nvarchar(40)),
                   CAST(REPLICATE(CAST('z' AS varchar(max)), 4000 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
                       AS varbinary(max))
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        // Every value null, which no lab column has, plus one with a single non null
        await Build(connection, "SegAllNull", """
            CREATE TABLE SegAllNull (Id int NOT NULL, AllNull int NULL, AllNullText varchar(50) NULL,
                OneValue int NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_SegAllNull ON SegAllNull;
            INSERT INTO SegAllNull WITH (TABLOCK)
            SELECT TOP (20000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int), NULL, NULL,
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) = 1 THEN 42 ELSE NULL END
            FROM sys.all_columns a CROSS JOIN sys.all_columns b;
            """);

        foreach (var tableName in new[] { "SegEdgeTypes", "SegLadder", "SegAllNull" })
        {
            await Compress(connection, tableName);
        }
    }

    private static async Task Compress(SqlConnection connection, string tableName)
    {
        await using var command = new SqlCommand(
            $"ALTER INDEX ALL ON {tableName} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 900
        };

        await command.ExecuteNonQueryAsync();
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
