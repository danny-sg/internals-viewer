using System.Globalization;
using System.Text.RegularExpressions;
using InternalsViewer.Query.Events.BatchMode.Enums;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Query.Memory;

internal static partial class ObjectPoolReader
{
    private const string SnapshotSql = """
                                       SELECT entry_address, entry_data_address, memory_object_address, pages_kb, entry_data
                                       FROM   sys.dm_os_memory_cache_entries
                                       WHERE  type = 'CACHESTORE_COLUMNSTOREOBJECTPOOL';
                                       """;

    private const string ObjectTypeAttribute = "object_type";

    private const string DatabaseIdAttribute = "db_id";

    private const string HobtIdAttribute = "hobt_id";

    private const string ColumnIdAttribute = "column_id";

    private const string ObjectIdAttribute = "object_id";

    private const string ObjectSizeAttribute = "object_size";

    public static async Task<ObjectPoolSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(SnapshotSql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var entries = new List<ObjectPoolEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetValue(0) is not byte[] entryAddress
                || reader.GetValue(1) is not byte[] dataAddress
                || reader.IsDBNull(4))
            {
                continue;
            }

            var memoryObjectAddress = reader.GetValue(2) is byte[] memoryObject ? MemoryClerkReader.AddressOf(memoryObject) : 0;

            var pagesKb = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);

            if (Parse(MemoryClerkReader.AddressOf(entryAddress),
                      MemoryClerkReader.AddressOf(dataAddress),
                      memoryObjectAddress,
                      pagesKb,
                      reader.GetString(4)) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return new ObjectPoolSnapshot(entries);
    }

    internal static ObjectPoolEntry? Parse(ulong entryAddress, ulong dataAddress, ulong memoryObjectAddress, long pagesKb, string entryData)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in AttributeRegex().Matches(entryData))
        {
            attributes[match.Groups[1].Value] = match.Groups[2].Value;
        }

        if (!TryGet(attributes, ObjectTypeAttribute, out int objectType) || !TryGet(attributes, HobtIdAttribute, out ulong hobtId))
        {
            return null;
        }

        return new ObjectPoolEntry(entryAddress,
                                   dataAddress,
                                   memoryObjectAddress,
                                   pagesKb,
                                   (ColumnStoreObjectType)objectType,
                                   TryGet(attributes, DatabaseIdAttribute, out int databaseId) ? databaseId : 0,
                                   hobtId,
                                   TryGet(attributes, ColumnIdAttribute, out int columnId) ? columnId : -1,
                                   TryGet(attributes, ObjectIdAttribute, out int objectId) ? objectId : -1,
                                   TryGet(attributes, ObjectSizeAttribute, out long objectSize) ? objectSize : 0);
    }

    private static bool TryGet<T>(Dictionary<string, string> attributes, string name, out T value) where T : struct, IParsable<T>
        => T.TryParse(attributes.GetValueOrDefault(name), CultureInfo.InvariantCulture, out value);

    [GeneratedRegex(@"(\w+)\s*=\s*'([^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();
}
