using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Catalog metadata for the table whose segments the RLE entry width rule got wrong
/// </summary>
public sealed class ScaledWidthProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Scaled_Width_Metadata()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT c.name, TYPE_NAME(c.user_type_id), c.precision, c.scale,
                                                        s.segment_id, s.encoding_type, s.row_count,
                                                        s.base_id, s.magnitude, s.min_data_id, s.max_data_id,
                                                        s.primary_dictionary_id, s.on_disk_size
                                                 FROM sys.column_store_segments s
                                                 JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                 JOIN sys.columns c ON c.object_id = p.object_id
                                                      AND c.column_id = s.column_id
                                                 WHERE p.object_id = OBJECT_ID('SegScaled')
                                                 ORDER BY s.segment_id, s.column_id
                                                 """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var baseId = Convert.ToInt64(reader.GetValue(7));

            var magnitude = Convert.ToDouble(reader.GetValue(8));

            var max = Convert.ToInt64(reader.GetValue(10));

            var scaled = baseId >= 0 && magnitude > 0;

            var storedMax = scaled ? (max / magnitude) - baseId : 0;

            _lines.Add($"{reader.GetValue(0),-8} {reader.GetValue(1),-10} p{reader.GetValue(2)} s{reader.GetValue(3)} "
                       + $"rg{reader.GetValue(4)} enc {reader.GetValue(5)} rows {reader.GetValue(6)} "
                       + $"base {baseId} magnitude {magnitude} min {reader.GetValue(9)} max {max} "
                       + $"dictionary {reader.GetValue(11)} storedMax {storedMax:N0} "
                       + $"predictedWide {(scaled && storedMax > int.MaxValue)}");
        }

        ProbeDump.Write("scaled_width_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
