using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Reports how many locator columns a nonclustered columnstore keeps over a composite clustered key
/// </summary>
public sealed class CompositeKeyLocatorProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Report_Composite_Key_Locators()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand("SELECT OBJECT_ID('SegNciComposite')", connection))
        {
            if (await exists.ExecuteScalarAsync() is null or DBNull)
            {
                await Build(connection);
            }
        }

        await Dump(connection,
                   """
                   SELECT t.name AS TableName, s.column_id AS SegmentColumnId,
                          c.name AS IndexColumn, ty.name AS TypeName,
                          s.encoding_type AS Encoding, s.on_disk_size AS Size
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.indexes i ON i.object_id = p.object_id AND i.index_id = p.index_id
                   LEFT JOIN sys.index_columns ic ON ic.object_id = t.object_id
                                                 AND ic.index_id = i.index_id
                                                 AND ic.index_column_id = s.column_id
                   LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
                   LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                   WHERE t.name LIKE 'SegNciComp%' AND s.segment_id = 0
                   ORDER BY t.name, s.column_id
                   """);

        await Dump(connection,
                   """
                   SELECT t.name AS TableName, i.name AS IndexName, i.type_desc,
                          ic.index_column_id, c.name AS ColumnName, ic.is_included_column
                   FROM sys.indexes i
                   JOIN sys.tables t ON t.object_id = i.object_id
                   JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
                   WHERE t.name LIKE 'SegNciComp%'
                   ORDER BY t.name, i.index_id, ic.index_column_id
                   """);
    }

    private static async Task Build(SqlConnection connection)
    {
        await Execute(connection,
                      """
                      CREATE TABLE SegNciComposite
                      (
                          KeyA int NOT NULL,
                          KeyB int NOT NULL,
                          KeyC date NOT NULL,
                          Val int NOT NULL,
                          Note varchar(20) NOT NULL,
                          CONSTRAINT PK_SegNciComposite PRIMARY KEY CLUSTERED (KeyA, KeyB, KeyC)
                      )
                      """);

        await Execute(connection,
                      """
                      INSERT INTO SegNciComposite
                      SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 1, '2024-01-01',
                             ABS(CHECKSUM(NEWID())) % 50, 'row'
                      FROM sys.all_columns a CROSS JOIN sys.all_columns b
                      """);

        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Composite ON SegNciComposite (Val, Note)");

        // A key column already in the index answers whether the locator repeats it
        await Execute(connection,
                      """
                      CREATE TABLE SegNciCompOverlap
                      (
                          KeyA int NOT NULL,
                          KeyB int NOT NULL,
                          Val int NOT NULL,
                          CONSTRAINT PK_SegNciCompOverlap PRIMARY KEY CLUSTERED (KeyA, KeyB)
                      )
                      """);

        await Execute(connection,
                      """
                      INSERT INTO SegNciCompOverlap
                      SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), 1, ABS(CHECKSUM(NEWID())) % 50
                      FROM sys.all_columns a CROSS JOIN sys.all_columns b
                      """);

        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Overlap ON SegNciCompOverlap (KeyA, Val)");
    }

    private async Task Dump(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await using var reader = await command.ExecuteReaderAsync();

        TestOutput.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));

        while (await reader.ReadAsync())
        {
            var builder = new StringBuilder();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                builder.Append(i > 0 ? " | " : string.Empty).Append(reader.IsDBNull(i) ? "(null)" : reader.GetValue(i));
            }

            TestOutput.WriteLine(builder.ToString());
        }

        TestOutput.WriteLine(string.Empty);
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 300 };

        await command.ExecuteNonQueryAsync();
    }
}
