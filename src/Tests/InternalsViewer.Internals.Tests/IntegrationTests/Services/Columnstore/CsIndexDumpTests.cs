using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Dumps what CSINDEX reports for a segment and for a dictionary, which is the engine's own account of their layout
/// </summary>
/// <remarks>
/// CSINDEX writes to the message stream rather than returning a result set, so the output is picked up off the
/// connection's info message event with trace flag 3604 turned on to route it to the client in the first place.
///
/// The argument list is DBCC CSINDEX(database id, hobt id, column, target, kind, reserved), worked out by sweeping
/// it. Everything the call takes has to be a literal, so DB_ID() and the like have to be resolved first. Two of the
/// arguments do not mean what their position suggests:
///
/// - the column runs ONE AHEAD of the column id the catalog reports, so catalog column 2 is column 3 here
/// - kind 1 dumps a segment and reads the target as a row group, kind 4 dumps a dictionary and reads it as a
///   dictionary id. No other value of kind returns anything
/// </remarks>
public sealed class CsIndexDumpTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private const string TableName = "SegLocalDict";

    private const int SegmentKind = 1;

    private const int LocalDictionaryKind = 4;

    private const int GlobalDictionaryKind = 2;

    [RequiresConnectionStringFact("local")]
    public async Task Dump_String_Dictionary_Sub_Lobs()
    {
        var messages = new List<string>();

        await using var connection = await Connect(messages);

        var databaseId = await GetDatabaseId(connection);

        var targets = new List<(long Hobt, int Column, int RowGroup, int Dictionary, string Label)>();

        await using (var command = new SqlCommand("""
                                                  SELECT s.hobt_id, s.column_id, s.segment_id,
                                                         s.primary_dictionary_id, s.secondary_dictionary_id,
                                                         OBJECT_NAME(p.object_id) + '.' + c.name
                                                  FROM sys.column_store_segments s
                                                  JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                  JOIN sys.columns c ON c.object_id = p.object_id AND c.column_id = s.column_id
                                                  WHERE s.encoding_type = 3
                                                    AND s.segment_id = 0
                                                    AND (s.primary_dictionary_id >= 0 OR s.secondary_dictionary_id >= 0)
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var primary = reader.GetInt32(3);

                var secondary = reader.GetInt32(4);

                targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2),
                             primary >= 0 ? primary : secondary,
                             reader.GetString(5) + (primary >= 0 ? " global" : " local")));
            }
        }

        foreach (var target in targets.Take(3))
        {
            await Execute(connection,
                          $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, {target.RowGroup}, {SegmentKind}, 0)");

            messages.Clear();

            await Execute(connection,
                          $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, 0, {GlobalDictionaryKind}, 0)");

            TestOutput.WriteLine($"=== {target.Label} ({messages.Count} messages) ===");

            foreach (var line in messages.Select(m => m.TrimEnd()))
            {
                var trimmed = line.Trim();

                if (trimmed.Length > 0 && !trimmed.StartsWith("Index ") && !trimmed.StartsWith("Handle "))
                {
                    TestOutput.WriteLine(line);
                }
            }
        }

    }

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Dictionary_Sub_Lobs()
    {
        var messages = new List<string>();

        await using var connection = await Connect(messages);

        var databaseId = await GetDatabaseId(connection);

        foreach (var target in await GetDictionaries(connection))
        {
            messages.Clear();

            // CSINDEX reports what the columnstore object pool is holding, so the segment is dumped first to load it
            await Execute(connection,
                          $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, {target.Dictionary - 1}, {SegmentKind}, 0)");

            messages.Clear();

            await Execute(connection,
                          $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, {target.Dictionary}, {LocalDictionaryKind}, 0)");

            TestOutput.WriteLine($"=== {TableName} column {target.Column} dictionary {target.Dictionary} ({messages.Count} messages) ===");

            // The value array runs to thousands of lines, and the headers are the point
            foreach (var line in messages.TakeWhile(m => !m.Contains("Array Data")).Select(m => m.TrimEnd()))
            {
                if (line.Trim().Length > 0)
                {
                    TestOutput.WriteLine(line);
                }
            }
        }
    }

    private async Task<List<(long Hobt, int Column, int Dictionary)>> GetDictionaries(SqlConnection connection)
    {
        var targets = new List<(long, int, int)>();

        await using var command = new SqlCommand($"""
                                                  SELECT DISTINCT s.hobt_id, s.column_id, s.secondary_dictionary_id
                                                  FROM sys.column_store_segments s
                                                  JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                  WHERE OBJECT_NAME(p.object_id) = '{TableName}'
                                                    AND s.secondary_dictionary_id >= 0
                                                  ORDER BY s.column_id, s.secondary_dictionary_id
                                                  """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2)));
        }

        return targets;
    }

    private async Task<SqlConnection> Connect(List<string> messages)
    {
        var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        connection.FireInfoMessageEventOnUserErrors = true;

        await connection.OpenAsync();

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        return connection;
    }

    private static async Task<int> GetDatabaseId(SqlConnection connection)
    {
        await using var command = new SqlCommand("SELECT DB_ID()", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await command.ExecuteNonQueryAsync();
    }
}
