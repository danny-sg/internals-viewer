using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Builds a store by value column whose repeats fall in the MIDDLE of its values, to get two read runs
/// </summary>
/// <remarks>
/// Every multi run segment in the lab so far starts with a null run, so nothing separates "the first run starts
/// at zero" from "a segment with several runs continues". A column that repeats in the middle puts a read run
/// first and more runs after it, which tells the two apart.
/// </remarks>
public sealed class TwoReadRunProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegTwoReads";

    private const string SpreadTable = "SegSpreadRuns";

    private const string OrderedTable = "SegOrderedRuns";

    private const string ManyTable = "SegRunLadder";

    private const string SizesTable = "SegRunSizes";

    private const string ConflictTable = "SegConflict";

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Two_Read_Runs()
    {
        await BuildTable();

        var database = await LoadDatabase();

        var service = CreateService();

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == TableName);

        var index = await service.GetIndex(allocationUnit, database, CancellationToken.None);

        var rowGroup = index.CompressedRowGroups.FirstOrDefault();

        if (rowGroup is null)
        {
            _lines.Add("no compressed row groups");

            Write();

            return;
        }

        foreach (var (columnId, columnName) in new[] { (4, "MidRepeat"), (5, "MidNull") })
        {
            await Report(service, database, index, rowGroup, columnId, columnName);
        }

        await BuildSpread();

        var spreadDatabase = await LoadDatabase();

        var spreadUnit = spreadDatabase.AllocationUnits.Values.First(a => a.TableName == SpreadTable);

        var spreadIndex = await service.GetIndex(spreadUnit, spreadDatabase, CancellationToken.None);

        foreach (var spreadGroup in spreadIndex.CompressedRowGroups)
        {
            await Report(service, spreadDatabase, spreadIndex, spreadGroup, 3, "Spread", SpreadTable);
        }

        await BuildOrdered();

        var orderedDatabase = await LoadDatabase();

        var orderedUnit = orderedDatabase.AllocationUnits.Values.First(a => a.TableName == OrderedTable);

        var orderedIndex = await service.GetIndex(orderedUnit, orderedDatabase, CancellationToken.None);

        foreach (var orderedGroup in orderedIndex.CompressedRowGroups)
        {
            await Report(service, orderedDatabase, orderedIndex, orderedGroup, 3, "Payload", OrderedTable);
        }

        await BuildMany();

        var manyDatabase = await LoadDatabase();

        var manyUnit = manyDatabase.AllocationUnits.Values.First(a => a.TableName == ManyTable);

        var manyIndex = await service.GetIndex(manyUnit, manyDatabase, CancellationToken.None);

        _lines.Add($"=== {ManyTable} groups compressed {manyIndex.CompressedRowGroups.Count()} "
                   + $"all {manyIndex.RowGroups.Count} ===");

        foreach (var manyGroup in manyIndex.CompressedRowGroups)
        {
            await Report(service, manyDatabase, manyIndex, manyGroup, 3, "Payload", ManyTable);
        }

        await BuildSizes();

        var sizesDatabase = await LoadDatabase();

        var sizesUnit = sizesDatabase.AllocationUnits.Values.First(a => a.TableName == SizesTable);

        var sizesIndex = await service.GetIndex(sizesUnit, sizesDatabase, CancellationToken.None);

        foreach (var sizesGroup in sizesIndex.CompressedRowGroups)
        {
            await Report(service, sizesDatabase, sizesIndex, sizesGroup, 4, "Payload", SizesTable);
        }

        await BuildConflict();

        var conflictDatabase = await LoadDatabase();

        var conflictUnit = conflictDatabase.AllocationUnits.Values.First(a => a.TableName == ConflictTable);

        var conflictIndex = await service.GetIndex(conflictUnit, conflictDatabase, CancellationToken.None);

        foreach (var conflictGroup in conflictIndex.CompressedRowGroups)
        {
            foreach (var (columnId, columnName) in new[] { (3, "Alpha"), (4, "Beta"), (5, "Gamma") })
            {
                await Report(service, conflictDatabase, conflictIndex, conflictGroup, columnId, columnName, ConflictTable);
            }
        }

        var wideUnit = database.AllocationUnits.Values.FirstOrDefault(a => a.TableName == "SegWideNull");

        if (wideUnit is not null)
        {
            var wideIndex = await service.GetIndex(wideUnit, database, CancellationToken.None);

            foreach (var wideGroup in wideIndex.CompressedRowGroups)
            {
                foreach (var (columnId, columnName) in new[] { (3, "Guid1"), (4, "Bin20"), (5, "Dto") })
                {
                    await Report(service, database, wideIndex, wideGroup, columnId, columnName, "SegWideNull");
                }
            }
        }

        Write();
    }

    private async Task Report(ColumnstoreService service,
                              DatabaseSource database,
                              InternalsViewer.Internals.Columnstore.Metadata.ColumnStoreIndex index,
                              InternalsViewer.Internals.Columnstore.Metadata.RowGroup rowGroup,
                              int columnId,
                              string columnName,
                              string tableName = TableName)
    {
        var segment = rowGroup.Segments.FirstOrDefault(s => s.Key.ColumnId == columnId);

        if (segment is null)
        {
            _lines.Add($"=== {tableName}.{columnName}: no segment for column {columnId}, row group {rowGroup.RowGroupId} "
                       + $"has [{string.Join(", ", rowGroup.Segments.Select(s => s.Key.ColumnId))}] ===");

            await using var catalog = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

            await catalog.OpenAsync();

            await using (var shape = new SqlCommand($"""
                                                    SELECT DB_NAME(), (SELECT COUNT(*) FROM {tableName}),
                                                           (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{tableName}')),
                                                           (SELECT STRING_AGG(CAST(column_id AS varchar(4)) + ':' + name, ',')
                                                            FROM sys.columns WHERE object_id = OBJECT_ID('{tableName}'))
                                                    """, catalog))
            {
                await using var shaped = await shape.ExecuteReaderAsync();

                while (await shaped.ReadAsync())
                {
                    _lines.Add($"  database {shaped.GetValue(0)} rows {shaped.GetValue(1)} "
                               + $"columns {shaped.GetValue(2)} [{shaped.GetValue(3)}]");
                }
            }

            await using var query = new SqlCommand($"""
                                                    SELECT s.segment_id, s.column_id, s.encoding_type, s.row_count,
                                                           s.on_disk_size, s.min_data_id, s.max_data_id
                                                    FROM sys.column_store_segments s
                                                    JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                    WHERE p.object_id = OBJECT_ID('{tableName}')
                                                    ORDER BY s.segment_id, s.column_id
                                                    """, catalog);

            await using var rows = await query.ExecuteReaderAsync();

            while (await rows.ReadAsync())
            {
                _lines.Add($"  catalog rg {rows.GetValue(0)} col {rows.GetValue(1)} enc {rows.GetValue(2)} "
                           + $"rows {rows.GetValue(3)} size {rows.GetValue(4)} "
                           + $"minId {rows.GetValue(5)} maxId {rows.GetValue(6)}");
            }

            return;
        }

        var blob = await service.GetSegmentBlob(database, segment, CancellationToken.None);

        var accumulated = 0;

        var runs = new List<string>();

        var divergence = string.Empty;

        var pageStarts = new List<int>();

        if (blob.VariableLengthData is { } pages)
        {
            var start = 0;

            foreach (var page in pages.Pages)
            {
                pageStarts.Add(start);

                start += page.ValueCount;
            }
        }

        for (var i = 0; i < blob.RleEntries.Length; i++)
        {
            var entry = blob.RleEntries[i];

            var page = (int)(entry.Value & 0x7FFF);

            var slot = (int)((entry.Value & 0x3FFF8000) >> 15);

            var resolved = page < pageStarts.Count ? pageStarts[page] + slot : -1;

            var text = $"[{i}] {(entry.Value < 0 ? "READ" : "REPEAT")} 0x{(uint)entry.Value:X8} x{entry.Count} "
                       + $"page {page} slot {slot} resolved {resolved} accumulated {accumulated}";

            if (entry.Count > 0 && resolved != accumulated && divergence.Length == 0)
            {
                divergence = $"  FIRST DIVERGENCE {text}";
            }

            if (blob.RleEntries.Length <= 12 || i < 3 || Math.Abs(i - 32768) < 4 || i >= blob.RleEntries.Length - 2)
            {
                runs.Add(text);
            }

            accumulated += entry.Value < 0 ? entry.Count : 1;
        }

        _lines.Add($"=== {tableName}.{columnName} enc {(int)segment.Encoding} structure {(int)blob.Header.RleType} "
                   + $"rows {segment.RowCount} ===");

        _lines.Add($"  runs ({blob.RleEntries.Length}) [{string.Join(" | ", runs)}]");

        _lines.Add(divergence.Length == 0
                       ? "  EVERY RUN RESOLVES TO ITS ACCUMULATED ORDINAL"
                       : divergence);

        var repeats = blob.RleEntries.Where(e => e.Value >= 0 && e.Count > 0).Select(e => e.Count).ToList();

        if (repeats.Count > 0)
        {
            _lines.Add($"  repeat run lengths min {repeats.Min()} max {repeats.Max()} count {repeats.Count} "
                       + $"shortest [{string.Join(", ", repeats.OrderBy(c => c).Take(12))}]");
        }

        if (blob.VariableLengthData is { } store)
        {
            _lines.Add($"  store valueCount {store.ValueCount} pages {store.PageCount} "
                       + $"variable {store.Pages.Count(p => p.IsVariableWidth)} "
                       + $"fixed {store.Pages.Count(p => !p.IsVariableWidth)}");

            _lines.Add($"  page value counts [{string.Join(", ", store.Pages.Select((p, i) => $"{i}:{p.ValueCount}"))}]");
        }

        var metadata = segment.LocalDictionary
                       ?? index.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == columnId)?.GlobalDictionary;

        if (metadata is null)
        {
            _lines.Add("  no dictionary");
        }
        else
        {
            try
            {
                var dictionary = await service.GetDictionaryBlob(database, metadata, CancellationToken.None);

                _lines.Add($"  dictionary type {metadata.Type} lobtype {(int)dictionary.LobType} "
                           + $"entries {dictionary.EntryCount} lastId {metadata.LastId} "
                           + $"blob {dictionary.GetType().Name}");
            }
            catch (Exception exception)
            {
                _lines.Add($"  dictionary: {exception.Message}");
            }
        }

        await using (var check = new SqlConnection(ConnectionStringHelper.GetConnectionString("local")))
        {
            await check.OpenAsync();

            await using var distinct = new SqlCommand(
                $"SELECT COUNT(DISTINCT {columnName}), COUNT(*) FROM {tableName}", check);

            await using var reading = await distinct.ExecuteReaderAsync();

            while (await reading.ReadAsync())
            {
                _lines.Add($"  table distinct {reading.GetValue(0)} of {reading.GetValue(1)}");
            }
        }

        if (blob.VariableLengthData is { } bookmarkStore && blob.Bookmarks.Length > 0)
        {
            var stream = new SegmentDataIdStream(blob);

            var bookmarkLines = new List<string>();

            var agree = 0;

            for (var i = 0; i < blob.Bookmarks.Length; i++)
            {
                var bookmark = blob.Bookmarks[i];

                var bookmarkPage = bookmark.Position & 0x7FFF;

                var bookmarkSlot = (bookmark.Position & 0x3FFF8000) >> 15;

                var address = bookmarkStore.GetOrdinal(bookmarkPage, bookmarkSlot);

                var endRow = Math.Min(bookmark.EndRow, segment.RowCount - 1);

                var (_, actual) = stream.FindValue(Math.Max(0, endRow));

                if (address == actual)
                {
                    agree++;
                }

                if (i < 4)
                {
                    bookmarkLines.Add($"[{i}] pos 0x{(uint)bookmark.Position:X8} page {bookmarkPage} "
                                      + $"slot {bookmarkSlot} address {address} endRow {bookmark.EndRow} "
                                      + $"ordinalAtEndRow {actual}");
                }
            }

            _lines.Add($"  bookmarks {blob.Bookmarks.Length} distance {blob.Header.BookmarkDistance} "
                       + $"agree {agree}/{blob.Bookmarks.Length}");

            foreach (var line in bookmarkLines)
            {
                _lines.Add($"    {line}");
            }
        }

        var readRuns = blob.RleEntries.Count(e => e.Value < 0);

        _lines.Add($"  READ RUNS {readRuns}");

        var idReader = await service.GetSegmentReader(database,
                                                      rowGroup.Segments.First(s => s.Key.ColumnId == 2),
                                                      CancellationToken.None);

        var reader = await service.GetSegmentReader(database, segment, CancellationToken.None);

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        // Sampled either side of every run boundary, which is where an accumulated ordinal would go wrong
        var boundaries = new List<int> { 0, 1 };

        var at = 0;

        foreach (var entry in blob.RleEntries.Where(e => e.Count > 0))
        {
            at += entry.Count;

            boundaries.Add(at - 2);
            boundaries.Add(at - 1);
            boundaries.Add(at);
            boundaries.Add(at + 1);
        }

        var matched = 0;

        var checkedRows = 0;

        var mismatch = string.Empty;

        foreach (var row in boundaries.Where(r => r >= 0 && r < segment.RowCount).Distinct().OrderBy(r => r))
        {
            var decoded = reader.GetValue(row);

            await using var command = new SqlCommand(
                $"SELECT CONVERT(varchar(60), {columnName}) FROM {tableName} WHERE Id = @id", connection);

            command.Parameters.AddWithValue("@id", idReader.GetValue(row) ?? (object)DBNull.Value);

            var actual = await command.ExecuteScalarAsync();

            var expected = actual is null or DBNull ? null : actual.ToString();

            var got = decoded?.ToString();

            checkedRows++;

            if (string.Equals(got, expected, StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
            else if (mismatch.Length == 0)
            {
                mismatch = $" FIRST MISMATCH row {row} decoded '{got ?? "<null>"}' actual '{expected ?? "<null>"}'";
            }
        }

        _lines.Add($"  MATCHED {matched}/{checkedRows}{mismatch}");
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

    private async Task BuildTable()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{TableName}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        // Band is what the reordering has most to gain from, so the guid columns follow its ordering rather than
        // setting their own, which puts their repeats in the middle rather than at the front
        var script = $"""
            CREATE TABLE {TableName} (Id int NOT NULL, Band int NOT NULL,
                MidRepeat uniqueidentifier NOT NULL, MidNull uniqueidentifier NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{TableName} ON {TableName};
            INSERT INTO {TableName} WITH (TABLOCK)
            SELECT TOP (60000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   CAST((ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) / 10000 AS int),
                   CASE WHEN (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) / 10000 = 3
                        THEN CAST('11111111-2222-3333-4444-555555555555' AS uniqueidentifier)
                        ELSE NEWID() END,
                   CASE WHEN (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) / 10000 = 3
                        THEN NULL ELSE NEWID() END
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 900 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {TableName} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 900
        };

        await compress.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Three repeated values spread across the value range, so the ordering cannot gather them at the front
    /// </summary>
    /// <remarks>
    /// A guid sorts on its trailing bytes in SQL Server, so the constants are chosen low, middle and high there.
    /// If the blocks land apart the reads between them are separate runs, which is the shape being hunted.
    /// </remarks>
    private async Task BuildSpread()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{SpreadTable}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {SpreadTable} (Id int NOT NULL, Spread uniqueidentifier NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{SpreadTable} ON {SpreadTable};
            INSERT INTO {SpreadTable} WITH (TABLOCK)
            SELECT TOP (60000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int),
                   CASE (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) % 6
                        WHEN 0 THEN CAST('00000000-0000-0000-0000-000000000001' AS uniqueidentifier)
                        WHEN 1 THEN CAST('00000000-0000-0000-0000-800000000000' AS uniqueidentifier)
                        WHEN 2 THEN CAST('00000000-0000-0000-0000-FFFFFFFFFFFE' AS uniqueidentifier)
                        ELSE NEWID() END
            FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 900 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {SpreadTable} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 900
        };

        await compress.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// An ordered index, so the row order is the sort column rather than whatever suits the payload column
    /// </summary>
    /// <remarks>
    /// Repeated blocks sit in the middle of the sort order, which is the only lever found that stops them being
    /// gathered at the front, and therefore the only way to get a repeat run after a read run.
    /// </remarks>
    private async Task BuildOrdered()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{OrderedTable}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {OrderedTable} (Id int NOT NULL, Payload uniqueidentifier NOT NULL);
            INSERT INTO {OrderedTable} WITH (TABLOCK)
            SELECT Id, CASE WHEN Id BETWEEN 50000 AND 59999
                                 THEN CAST('00000000-0000-0000-0000-000000000001' AS uniqueidentifier)
                            WHEN Id BETWEEN 150000 AND 159999
                                 THEN CAST('00000000-0000-0000-0000-800000000000' AS uniqueidentifier)
                            WHEN Id BETWEEN 250000 AND 259999
                                 THEN CAST('00000000-0000-0000-0000-FFFFFFFFFFFE' AS uniqueidentifier)
                            ELSE NEWID() END
            FROM (SELECT TOP (300000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Source;
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{OrderedTable} ON {OrderedTable} ORDER (Id);
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Every value repeated twice, so the run count climbs past the 32768 the located field appears to scale by
    /// </summary>
    private async Task BuildMany()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{ManyTable}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {ManyTable} (Id int NOT NULL, Payload uniqueidentifier NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{ManyTable} ON {ManyTable};
            INSERT INTO {ManyTable} WITH (TABLOCK)
            SELECT Id, CAST(CONVERT(binary(16), (Id - 1) / 2) AS uniqueidentifier)
            FROM (SELECT TOP (160000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Source;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {ManyTable} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 1800
        };

        await compress.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Repeat groups of every size from one upwards, which shows the shortest repeat worth a run of its own
    /// </summary>
    /// <remarks>
    /// The singletons are there to keep the cardinality high enough that the column stays store by value rather
    /// than picking up a dictionary, which would put it in a different structure entirely.
    /// </remarks>
    private async Task BuildSizes()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{SizesTable}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {SizesTable} (Id int NOT NULL, GroupId int NOT NULL, Payload uniqueidentifier NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{SizesTable} ON {SizesTable};
            INSERT INTO {SizesTable} WITH (TABLOCK) (Id, GroupId, Payload)
            SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), Groups.g,
                   CAST(CONVERT(binary(16), Groups.g) AS uniqueidentifier)
            FROM (SELECT TOP (400) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS g
                  FROM sys.all_columns) AS Groups
            CROSS APPLY (SELECT TOP (400) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS r
                         FROM sys.all_columns) AS Repeats
            WHERE Repeats.r <= Groups.g;
            INSERT INTO {SizesTable} WITH (TABLOCK) (Id, GroupId, Payload)
            SELECT 1000000 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 0, NEWID()
            FROM (SELECT TOP (200000) 1 AS x
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Singles;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {SizesTable} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 1800
        };

        await compress.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Three store by value columns whose repeated blocks want incompatible orderings
    /// </summary>
    /// <remarks>
    /// One permutation serves the whole row group, so only the winning column gets a clean gather and the others
    /// are left in fragments. Fragments with distinct values between them are what a second read run needs.
    /// </remarks>
    private async Task BuildConflict()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{ConflictTable}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {ConflictTable} (Id int NOT NULL, Alpha uniqueidentifier NOT NULL,
                Beta uniqueidentifier NOT NULL, Gamma uniqueidentifier NOT NULL);
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{ConflictTable} ON {ConflictTable};
            INSERT INTO {ConflictTable} WITH (TABLOCK)
            SELECT Id,
                   CASE WHEN (Id / 10000) % 2 = 0
                        THEN CAST('AAAAAAAA-0000-0000-0000-000000000000' AS uniqueidentifier) ELSE NEWID() END,
                   CASE WHEN (Id / 5000) % 2 = 0
                        THEN CAST('BBBBBBBB-0000-0000-0000-000000000000' AS uniqueidentifier) ELSE NEWID() END,
                   CASE WHEN (Id / 2500) % 2 = 0
                        THEN CAST('CCCCCCCC-0000-0000-0000-000000000000' AS uniqueidentifier) ELSE NEWID() END
            FROM (SELECT TOP (60000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Source;
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }

        await using var compress = new SqlCommand(
            $"ALTER INDEX ALL ON {ConflictTable} REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON)", connection)
        {
            CommandTimeout = 1800
        };

        await compress.ExecuteNonQueryAsync();
    }

    private void Write()
    {
        ProbeDump.Write("two_read_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
