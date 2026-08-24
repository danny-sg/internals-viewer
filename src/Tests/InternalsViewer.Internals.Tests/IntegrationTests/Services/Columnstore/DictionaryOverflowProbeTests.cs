using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// A segment that names both a global and a local dictionary, and whether its ids reach past the global
/// </summary>
public sealed class DictionaryOverflowProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Dictionary_Overflow()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionStringHelper.GetConnectionString("local"))
        {
            InitialCatalog = "AdventureWorksDW2025",
            ConnectTimeout = 30
        };

        await using var connection = new SqlConnection(builder.ConnectionString);

        await connection.OpenAsync();

        await using (var command = new SqlCommand("""
                                                  SELECT o.name + '.' + c.name, d.dictionary_id, d.type,
                                                         d.entry_count, d.last_id, d.on_disk_size
                                                  FROM sys.column_store_dictionaries d
                                                  JOIN sys.partitions p ON p.partition_id = d.hobt_id
                                                  JOIN sys.objects o ON o.object_id = p.object_id
                                                  JOIN sys.columns c ON c.object_id = p.object_id
                                                       AND c.column_id = d.column_id
                                                  WHERE o.name = 'FactAdditionalInternationalProductDescription'
                                                  ORDER BY c.name, d.dictionary_id
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"dictionary {reader.GetValue(0)} id {reader.GetValue(1)} type {reader.GetValue(2)} "
                           + $"entries {reader.GetValue(3)} lastId {reader.GetValue(4)} size {reader.GetValue(5)}");
            }
        }

        await using (var command = new SqlCommand("""
                                                  SELECT o.name + '.' + c.name, s.segment_id, s.encoding_type,
                                                         s.row_count, s.primary_dictionary_id, s.secondary_dictionary_id,
                                                         s.min_data_id, s.max_data_id, s.null_value, s.has_nulls
                                                  FROM sys.column_store_segments s
                                                  JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                  JOIN sys.objects o ON o.object_id = p.object_id
                                                  JOIN sys.columns c ON c.object_id = p.object_id
                                                       AND c.column_id = s.column_id
                                                  WHERE o.name = 'FactAdditionalInternationalProductDescription'
                                                  ORDER BY c.name, s.segment_id
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"segment {reader.GetValue(0)} rg{reader.GetValue(1)} enc {reader.GetValue(2)} "
                           + $"rows {reader.GetValue(3)} primary {reader.GetValue(4)} secondary {reader.GetValue(5)} "
                           + $"minId {reader.GetValue(6)} maxId {reader.GetValue(7)} "
                           + $"nullValue {reader.GetValue(8)} hasNulls {reader.GetValue(9)}");
            }
        }

        ProbeDump.Write("dictionary_overflow_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
