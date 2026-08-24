using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Asks CSINDEX what it calls the field the viewer has been calling the structure type
/// </summary>
public sealed class LobTypeProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Rle_Headers()
    {
        var messages = new List<string>();

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        connection.FireInfoMessageEventOnUserErrors = true;

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        await connection.OpenAsync();

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        var databaseId = Convert.ToInt32(await Scalar(connection, "SELECT DB_ID()"));

        var targets = new List<(long Hobt, int Column, int RowGroup, int Encoding, string Table)>();

        await using (var command = new SqlCommand("""
                                                  SELECT hobt_id, column_id, segment_id, encoding_type, name
                                                  FROM (SELECT s.hobt_id, s.column_id, s.segment_id,
                                                               s.encoding_type, o.name,
                                                               ROW_NUMBER() OVER (PARTITION BY s.encoding_type
                                                                                  ORDER BY o.name) AS rn
                                                        FROM sys.column_store_segments s
                                                        JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                        JOIN sys.objects o ON o.object_id = p.object_id
                                                        WHERE s.encoding_type IN (4, 5)) AS Ranked
                                                  WHERE rn <= 40
                                                  ORDER BY encoding_type, name
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2),
                             reader.GetInt32(3), reader.GetString(4)));
            }
        }

        foreach (var (hobt, column, rowGroup, encoding, table) in targets)
        {
            messages.Clear();

            await Execute(connection, $"SELECT TOP (1) * FROM {table}");

            try
            {
                await Execute(connection, $"DBCC CSINDEX({databaseId}, {hobt}, {column + 1}, {rowGroup}, 1, 0)");
            }
            catch (Exception exception)
            {
                _lines.Add($"=== {table} col{column} rg{rowGroup}: {exception.Message} ===");

                continue;
            }

            var lines = messages.SelectMany(m => m.Split(Environment.NewLine))
                                .Select(m => m.TrimEnd())
                                .Where(m => m.Trim().Length > 0)
                                .ToList();

            var lobType = lines.FirstOrDefault(l => l.Contains("Lob type =")) ?? "no RLE header";

            _lines.Add($"encoding {encoding} {table} col{column} rg{rowGroup} | "
                       + $"{lobType.Split("  ")[0].Trim()}");
        }

        ProbeDump.Write("lob_type_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);

        return await command.ExecuteScalarAsync();
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await command.ExecuteNonQueryAsync();
    }
}
