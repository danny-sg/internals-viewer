using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Asks CSINDEX what a mixed fixed and variable store holds, print option 1 listing the values themselves
/// </summary>
public sealed class MixedStoreProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Dump_Mixed_Store()
    {
        var messages = new List<string>();

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        connection.FireInfoMessageEventOnUserErrors = true;

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        await connection.OpenAsync();

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        var databaseId = Convert.ToInt32(await Scalar(connection, "SELECT DB_ID()"));

        foreach (var (table, column, label) in new[] { ("SegWideNull", 3, "Bin20 mixed"),
                                                       ("SegWideNull", 2, "Guid1 all variable"),
                                                       ("SegWideNull", 4, "Dto mixed") })
        {
            var hobt = Convert.ToInt64(await Scalar(connection,
                $"SELECT TOP (1) hobt_id FROM sys.partitions WHERE object_id = OBJECT_ID('{table}') AND index_id = 1"));

            messages.Clear();

            await Execute(connection, $"SELECT TOP (1) * FROM {table}");

            try
            {
                await Execute(connection, $"DBCC CSINDEX({databaseId}, {hobt}, {column + 1}, 0, 1, 1)");
            }
            catch (Exception exception)
            {
                _lines.Add($"=== {label}: {exception.Message} ===");

                continue;
            }

            _lines.Add($"=== {label} ({messages.Count} messages) ===");

            var lines = messages.SelectMany(m => m.Split(Environment.NewLine))
                                .Select(m => m.TrimEnd())
                                .Where(m => m.Trim().Length > 0)
                                .ToList();

            // The headers matter, and then the opening of whatever the values section turns out to be
            foreach (var line in lines.Take(46))
            {
                _lines.Add($"  {line}");
            }

            _lines.Add($"  ... {lines.Count} lines total");

            var valueIndex = lines.FindIndex(l => l.Contains("VLD Page Data") || l.Contains("Page Data for page"));

            if (valueIndex >= 0)
            {
                foreach (var line in lines.Skip(valueIndex).Take(14))
                {
                    _lines.Add($"  > {line}");
                }
            }
        }

        ProbeDump.Write("mixed_store_probe.txt", _lines);

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
