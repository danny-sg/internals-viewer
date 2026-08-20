using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports how the engine encoded every columnstore column of the lab database and what dictionaries it built
/// </summary>
public sealed class ColumnstoreSqlProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Dump_Encodings_And_Dictionaries()
    {
        await Dump("""
                   SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS TypeName,
                          s.encoding_type AS Encoding, COUNT(*) AS Segments,
                          MAX(s.row_count) AS MaxRows, SUM(CAST(s.on_disk_size AS bigint)) AS Size
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.hobt_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = s.column_id
                   JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   GROUP BY t.name, c.name, ty.name, s.encoding_type
                   ORDER BY t.name, c.name
                   """);

        await Dump("""
                   SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS TypeName,
                          d.dictionary_id AS DictId, d.type AS DictType,
                          d.entry_count AS Entries, d.on_disk_size AS Size
                   FROM sys.column_store_dictionaries d
                   JOIN sys.partitions p ON p.hobt_id = d.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = d.column_id
                   JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   ORDER BY t.name, c.name, d.dictionary_id
                   """);
    }

    private async Task Dump(string sql)
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await using var reader = await command.ExecuteReaderAsync();

        TestOutput.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));

        while (await reader.ReadAsync())
        {
            var builder = new StringBuilder();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                builder.Append(i > 0 ? " | " : string.Empty).Append(reader.GetValue(i));
            }

            TestOutput.WriteLine(builder.ToString());
        }

        TestOutput.WriteLine(string.Empty);
    }
}
