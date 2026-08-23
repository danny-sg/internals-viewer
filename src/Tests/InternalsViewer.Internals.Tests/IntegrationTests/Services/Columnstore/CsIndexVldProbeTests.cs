using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

public sealed class CsIndexVldProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Dump_Segment_Headings()
    {
        var messages = new List<string>();

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        connection.FireInfoMessageEventOnUserErrors = true;

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        await connection.OpenAsync();

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        var databaseId = Convert.ToInt32(await new SqlCommand("SELECT DB_ID()", connection).ExecuteScalarAsync());

        var targets = new List<(long Hobt, int Column, int RowGroup, int Encoding, string Label)>();

        await using (var command = new SqlCommand("""
                                                  SELECT TOP (12) s.hobt_id, s.column_id, s.segment_id, s.encoding_type,
                                                         OBJECT_NAME(p.object_id) + '.' + c.name
                                                  FROM sys.column_store_segments s
                                                  JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                  JOIN sys.columns c ON c.object_id = p.object_id AND c.column_id = s.column_id
                                                  WHERE s.segment_id = 0
                                                  ORDER BY s.encoding_type DESC, OBJECT_NAME(p.object_id)
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2),
                             reader.GetInt32(3), reader.GetString(4)));
            }
        }

        var lines = new List<string>();

        foreach (var target in targets)
        {
            messages.Clear();

            await Execute(connection,
                          $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, {target.RowGroup}, 1, 0)");

            lines.Add($"=== {target.Label} encoding {target.Encoding} ({messages.Count} messages) ===");

            foreach (var line in messages.Select(m => m.TrimEnd()).Take(60))
            {
                if (line.Trim().Length > 0)
                {
                    lines.Add(line);
                }
            }
        }

        foreach (var line in lines)
        {
            TestOutput.WriteLine(line);
        }

        File.WriteAllLines(Path.Combine("C:", "ColumnstoreDump", "csindex_headings.txt"), lines);
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await command.ExecuteNonQueryAsync();
    }
}
