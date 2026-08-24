using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether an encoding decides the structure and whether it decides having a dictionary
/// </summary>
public sealed class EncodingDictionaryProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private static readonly string[] Databases = ["ColumnStoreLab", "AdventureWorksDW2025", "WideWorldImportersDW"];

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Encoding_Against_Dictionary()
    {
        foreach (var databaseName in Databases)
        {
            var builder = new SqlConnectionStringBuilder(ConnectionStringHelper.GetConnectionString("local"))
            {
                InitialCatalog = databaseName,
                ConnectTimeout = 30
            };

            await using var connection = new SqlConnection(builder.ConnectionString);

            try
            {
                await connection.OpenAsync();
            }
            catch (Exception exception)
            {
                _lines.Add($"=== {databaseName}: {exception.Message} ===");

                continue;
            }

            _lines.Add($"=== {databaseName} ===");

            await using var command = new SqlCommand("""
                                                     SELECT s.encoding_type,
                                                            COUNT(*),
                                                            SUM(CASE WHEN s.primary_dictionary_id >= 0 THEN 1 ELSE 0 END),
                                                            SUM(CASE WHEN s.secondary_dictionary_id >= 0 THEN 1 ELSE 0 END),
                                                            STRING_AGG(CAST(x.name AS varchar(400)), ', ')
                                                              WITHIN GROUP (ORDER BY x.name)
                                                     FROM sys.column_store_segments s
                                                     CROSS APPLY (SELECT DISTINCT TYPE_NAME(c.user_type_id) AS name
                                                                  FROM sys.partitions p
                                                                  JOIN sys.columns c ON c.object_id = p.object_id
                                                                       AND c.column_id = s.column_id
                                                                  WHERE p.partition_id = s.hobt_id) AS x
                                                     GROUP BY s.encoding_type
                                                     ORDER BY s.encoding_type
                                                     """, connection);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  encoding {reader.GetValue(0)} segments {reader.GetValue(1),8} "
                           + $"primaryDictionary {reader.GetValue(2),8} secondaryDictionary {reader.GetValue(3),8} "
                           + $"types {reader.GetValue(4)}");
            }
        }

        ProbeDump.Write("encoding_dictionary_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
