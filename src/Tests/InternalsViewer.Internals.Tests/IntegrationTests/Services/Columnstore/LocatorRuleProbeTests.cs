using System.Text;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Works out which clustered key columns a nonclustered columnstore keeps as locators, and in what order
/// </summary>
/// <remarks>
/// Each key column is given a distinct cardinality, so the dictionary entry count of a locator segment says which
/// key column it holds without having to trust the ordering.
/// </remarks>
public sealed class LocatorRuleProbeTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Report_Locator_Rule()
    {
        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using (var exists = new SqlCommand("SELECT OBJECT_ID('SegLocKeyFirst')", connection))
        {
            if (await exists.ExecuteScalarAsync() is null or DBNull)
            {
                await Build(connection);
            }
        }

        await Dump(connection,
                   """
                   SELECT t.name AS TableName, s.column_id AS SegCol, c.name AS IndexColumn,
                          s.encoding_type AS Enc, d.entry_count AS DictEntries, s.on_disk_size AS Size
                   FROM sys.column_store_segments s
                   JOIN sys.partitions p ON p.partition_id = s.hobt_id
                   JOIN sys.tables t ON t.object_id = p.object_id
                   JOIN sys.indexes i ON i.object_id = p.object_id AND i.index_id = p.index_id
                   LEFT JOIN sys.index_columns ic ON ic.object_id = t.object_id
                                                 AND ic.index_id = i.index_id
                                                 AND ic.index_column_id = s.column_id
                   LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
                   LEFT JOIN sys.column_store_dictionaries d ON d.hobt_id = s.hobt_id
                                                            AND d.column_id = s.column_id
                                                            AND d.dictionary_id = 0
                   WHERE t.name LIKE 'SegLocKey%' AND s.segment_id = 0
                   ORDER BY t.name, s.column_id
                   """);

        await Dump(connection,
                   """
                   SELECT t.name AS TableName, i.name AS IndexName, i.type_desc, i.is_unique,
                          ic.key_ordinal, c.name AS ColumnName, ic.is_included_column
                   FROM sys.indexes i
                   JOIN sys.tables t ON t.object_id = i.object_id
                   JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                   JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
                   WHERE t.name LIKE 'SegLocKey%'
                   ORDER BY t.name, i.index_id, ic.index_column_id
                   """);
    }

    /// <summary>
    /// KeyA runs to 5000 values, KeyB to 2 and KeyC to 10, so a locator's dictionary says which one it is
    /// </summary>
    private static async Task Build(SqlConnection connection)
    {
        foreach (var name in new[] { "SegLocKeyFirst", "SegLocKeyMid", "SegLocKeyAll" })
        {
            await Execute(connection,
                          $"""
                           CREATE TABLE {name}
                           (
                               KeyA int NOT NULL,
                               KeyB int NOT NULL,
                               KeyC int NOT NULL,
                               Val int NOT NULL,
                               CONSTRAINT PK_{name} PRIMARY KEY CLUSTERED (KeyA, KeyB, KeyC)
                           )
                           """);

            await Execute(connection,
                          $"""
                           INSERT INTO {name}
                           SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
                                  ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 2,
                                  ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 10,
                                  ABS(CHECKSUM(NEWID())) % 50
                           FROM sys.all_columns a CROSS JOIN sys.all_columns b
                           """);
        }

        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_First ON SegLocKeyFirst (KeyA, Val)");
        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Mid ON SegLocKeyMid (KeyB, Val)");
        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_All ON SegLocKeyAll (KeyA, KeyB, KeyC, Val)");

        await Execute(connection, "CREATE TABLE SegLocKeyNonUnique (KeyA int NOT NULL, Val int NOT NULL)");

        await Execute(connection,
                      """
                      INSERT INTO SegLocKeyNonUnique
                      SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) % 100,
                             ABS(CHECKSUM(NEWID())) % 50
                      FROM sys.all_columns a CROSS JOIN sys.all_columns b
                      """);

        await Execute(connection, "CREATE CLUSTERED INDEX CI_SegLocKeyNonUnique ON SegLocKeyNonUnique (KeyA)");
        await Execute(connection, "CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_NonUnique ON SegLocKeyNonUnique (Val)");
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
