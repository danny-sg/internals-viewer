using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// What the catalog says about the segments whose RLE entries came out wide against those that did not
/// </summary>
public sealed class RleWidthMetadataProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private readonly List<string> _lines = [];

    [RequiresConnectionStringFact("local")]
    public async Task Probe_Metadata_For_The_Width()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT o.name + '.' + c.name AS Segment,
                                                        s.segment_id, s.encoding_type,
                                                        TYPE_NAME(c.user_type_id) AS DataType,
                                                        c.max_length, c.precision, c.scale, c.is_nullable,
                                                        s.row_count, s.has_nulls, s.base_id, s.magnitude,
                                                        s.null_value, s.min_data_id, s.max_data_id,
                                                        s.primary_dictionary_id, s.secondary_dictionary_id,
                                                        s.on_disk_size
                                                 FROM sys.column_store_segments s
                                                 JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                 JOIN sys.objects o ON o.object_id = p.object_id
                                                 JOIN sys.columns c ON c.object_id = p.object_id
                                                      AND c.column_id = s.column_id
                                                 WHERE (o.name = 'SegHugeDict' AND s.column_id = 3)
                                                    OR (o.name = 'SegTypes' AND s.column_id = 9)
                                                    OR (o.name = 'SegEdgeTypes' AND s.column_id = 5)
                                                 ORDER BY o.name, s.segment_id
                                                 """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var fields = new List<string>();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                fields.Add($"{reader.GetName(i)}={reader.GetValue(i)}");
            }

            _lines.Add(string.Join(" | ", fields));
        }

        ProbeDump.Write("rle_width_metadata_probe.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }
}
