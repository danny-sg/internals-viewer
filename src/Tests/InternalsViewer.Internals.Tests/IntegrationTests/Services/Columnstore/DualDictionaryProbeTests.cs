using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Tries to make a column carry a global and a local dictionary at once, the shape a segment overflows into
/// </summary>
/// <remarks>
/// The one real example found is AdventureWorks, whose index was built over a populated table rather than loaded
/// into an existing index. These build the same way, with long values so a dictionary reaches whatever cap makes
/// it spill.
/// </remarks>
public sealed class DualDictionaryProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Dual_Dictionary()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await BuildStaged(connection, "SegDualD");

        await Report(connection, "SegDualD");

        ProbeDump.Write("dual_dictionary_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private async Task Report(SqlConnection connection, string table)
    {
        _lines.Add($"=== {table} ===");

        await using (var command = new SqlCommand($"""
                                                   SELECT c.name, d.dictionary_id, d.type, d.entry_count,
                                                          d.last_id, d.on_disk_size
                                                   FROM sys.column_store_dictionaries d
                                                   JOIN sys.partitions p ON p.partition_id = d.hobt_id
                                                   JOIN sys.columns c ON c.object_id = p.object_id
                                                        AND c.column_id = d.column_id
                                                   WHERE p.object_id = OBJECT_ID('{table}')
                                                   ORDER BY c.name, d.dictionary_id
                                                   """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  dictionary {reader.GetValue(0)} id {reader.GetValue(1)} type {reader.GetValue(2)} "
                           + $"entries {reader.GetValue(3)} lastId {reader.GetValue(4)} size {reader.GetValue(5)}");
            }
        }

        await using (var command = new SqlCommand($"""
                                                   SELECT c.name, s.segment_id, s.encoding_type, s.row_count,
                                                          s.primary_dictionary_id, s.secondary_dictionary_id,
                                                          s.min_data_id, s.max_data_id
                                                   FROM sys.column_store_segments s
                                                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                   JOIN sys.columns c ON c.object_id = p.object_id
                                                        AND c.column_id = s.column_id
                                                   WHERE p.object_id = OBJECT_ID('{table}')
                                                   ORDER BY c.name, s.segment_id
                                                   """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  segment {reader.GetValue(0)} rg{reader.GetValue(1)} enc {reader.GetValue(2)} "
                           + $"rows {reader.GetValue(3)} primary {reader.GetValue(4)} secondary {reader.GetValue(5)} "
                           + $"minId {reader.GetValue(6)} maxId {reader.GetValue(7)}");
            }
        }
    }

    /// <summary>
    /// Each row group draws from its own block of values, so a later one meets values no earlier one held
    /// </summary>
    private static async Task BuildStaged(SqlConnection connection, string table)
    {
        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{table}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        var script = $"""
            CREATE TABLE {table} (Id int NOT NULL, Note nvarchar(400) NOT NULL);
            INSERT INTO {table} WITH (TABLOCK)
            SELECT Id, REPLICATE(N'v', 60) + CAST((Id / 30) + ((Id / 1048576) * 100000) AS nvarchar(20))
            FROM (SELECT TOP (2200000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c) AS Source;
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{table} ON {table};
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 3600 };

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task Build(SqlConnection connection, string table, int distinct, int width, int rows)
    {
        await using (var exists = new SqlCommand($"SELECT OBJECT_ID('{table}')", connection))
        {
            if (await exists.ExecuteScalarAsync() is not (null or DBNull))
            {
                return;
            }
        }

        // The index is built over a populated heap, which is how the one real example was made
        var script = $"""
            CREATE TABLE {table} (Id int NOT NULL, Note nvarchar(1000) NOT NULL);
            INSERT INTO {table} WITH (TABLOCK)
            SELECT Id, REPLICATE(CAST(CHAR(65 + (Id % {distinct}) % 26) AS nvarchar(10)), {width})
                       + CAST(Id % {distinct} AS nvarchar(10))
            FROM (SELECT TOP ({rows}) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS Id
                  FROM sys.all_columns a CROSS JOIN sys.all_columns b) AS Source;
            CREATE CLUSTERED COLUMNSTORE INDEX CCI_{table} ON {table};
            """;

        foreach (var batch in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 1800 };

            await command.ExecuteNonQueryAsync();
        }
    }
}
