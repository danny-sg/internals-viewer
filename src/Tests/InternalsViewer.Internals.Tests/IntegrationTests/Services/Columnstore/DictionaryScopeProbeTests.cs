using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Whether a column ever carries both a global and a local dictionary, which is what an overflow would look like
/// </summary>
public sealed class DictionaryScopeProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private static readonly string[] Databases = ["ColumnStoreLab", "AdventureWorksDW2025", "WideWorldImportersDW"];

    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Dictionary_Scope()
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

            await using (var command = new SqlCommand("""
                                                      SELECT o.name + '.' + c.name AS Column_,
                                                             SUM(CASE WHEN d.dictionary_id = 0 THEN 1 ELSE 0 END) AS Globals,
                                                             SUM(CASE WHEN d.dictionary_id > 0 THEN 1 ELSE 0 END) AS Locals,
                                                             MAX(CASE WHEN d.dictionary_id = 0 THEN d.entry_count END) AS GlobalEntries,
                                                             MAX(CASE WHEN d.dictionary_id > 0 THEN d.entry_count END) AS LargestLocal,
                                                             MAX(CASE WHEN d.dictionary_id = 0 THEN d.on_disk_size END) AS GlobalSize
                                                      FROM sys.column_store_dictionaries d
                                                      JOIN sys.partitions p ON p.partition_id = d.hobt_id
                                                      JOIN sys.objects o ON o.object_id = p.object_id
                                                      JOIN sys.columns c ON c.object_id = p.object_id
                                                           AND c.column_id = d.column_id
                                                      GROUP BY o.name, c.name
                                                      HAVING SUM(CASE WHEN d.dictionary_id > 0 THEN 1 ELSE 0 END) > 0
                                                      ORDER BY o.name, c.name
                                                      """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();

                var any = false;

                while (await reader.ReadAsync())
                {
                    any = true;

                    _lines.Add($"  {reader.GetValue(0),-34} globals {reader.GetValue(1)} locals {reader.GetValue(2)} "
                               + $"globalEntries {reader.GetValue(3)} largestLocal {reader.GetValue(4)} "
                               + $"globalSize {reader.GetValue(5)}");
                }

                if (!any)
                {
                    _lines.Add("  no column carries a local dictionary");
                }
            }

            await using (var command = new SqlCommand("""
                                                      SELECT TOP (6) o.name + '.' + c.name, s.segment_id,
                                                             s.primary_dictionary_id, s.secondary_dictionary_id,
                                                             s.row_count
                                                      FROM sys.column_store_segments s
                                                      JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                      JOIN sys.objects o ON o.object_id = p.object_id
                                                      JOIN sys.columns c ON c.object_id = p.object_id
                                                           AND c.column_id = s.column_id
                                                      WHERE s.secondary_dictionary_id >= 0
                                                      ORDER BY o.name, c.name, s.segment_id
                                                      """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    _lines.Add($"    segment {reader.GetValue(0)} rg{reader.GetValue(1)} "
                               + $"primary {reader.GetValue(2)} secondary {reader.GetValue(3)} "
                               + $"rows {reader.GetValue(4)}");
                }
            }
        }

        ProbeDump.Write("dictionary_scope_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
