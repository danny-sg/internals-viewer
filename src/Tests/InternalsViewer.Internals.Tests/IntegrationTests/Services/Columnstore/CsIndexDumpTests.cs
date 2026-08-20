using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Dumps what CSINDEX reports for a dictionary, which is the engine's own account of the elements it holds
/// </summary>
/// <remarks>
/// CSINDEX writes to the message stream rather than returning a result set, so the output is picked up off the
/// connection's info message event with trace flag 3604 turned on to route it to the client in the first place.
/// </remarks>
public sealed class CsIndexDumpTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Dump_Dictionary_Sub_Lobs()
    {
        var messages = new List<string>();

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        connection.FireInfoMessageEventOnUserErrors = true;

        await connection.OpenAsync();

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        var targets = new List<(long Hobt, int Column, int RowGroup, int Dictionary, string Table)>();

        await using (var command = new SqlCommand("""
                                                  SELECT d.hobt_id, d.column_id, rg.row_group_id, d.dictionary_id,
                                                         OBJECT_NAME(p.object_id)
                                                  FROM sys.column_store_dictionaries d
                                                  JOIN sys.partitions p ON p.partition_id = d.hobt_id
                                                  JOIN sys.column_store_row_groups rg
                                                       ON rg.object_id = p.object_id AND rg.state_desc = 'COMPRESSED'
                                                  WHERE OBJECT_NAME(p.object_id) = 'SegLocalDict'
                                                    AND rg.row_group_id = d.dictionary_id - 1
                                                  ORDER BY d.column_id, d.dictionary_id
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetString(4)));
            }
        }

        TestOutput.WriteLine($"{targets.Count} dictionaries to dump");

        foreach (var target in targets)
        {
            messages.Clear();

            var sql = $"DBCC CSINDEX({{0}}, {target.Hobt}, {target.Column}, {target.RowGroup}, 1, {target.Dictionary})";

            TestOutput.WriteLine(string.Empty);
            TestOutput.WriteLine($"=== {target.Table} column {target.Column} row group {target.RowGroup} dictionary {target.Dictionary} ===");

            try
            {
                await Execute(connection, string.Format(sql, "DB_ID()"));
            }
            catch (SqlException exception)
            {
                TestOutput.WriteLine($"[failed] {exception.Message}");
            }

            foreach (var message in messages)
            {
                TestOutput.WriteLine(message);
            }
        }
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await command.ExecuteNonQueryAsync();
    }
}
