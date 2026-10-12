using Microsoft.Data.SqlClient;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public static class MemoryClerks
{
    private const string SnapshotSql = """
                                       SELECT memory_clerk_address, type
                                       FROM   sys.dm_os_memory_clerks;

                                       SELECT o.memory_object_address, c.type
                                       FROM   sys.dm_os_memory_objects o
                                              INNER JOIN sys.dm_os_memory_clerks c ON c.page_allocator_address = o.page_allocator_address;

                                       SELECT page_allocator_address, type
                                       FROM   sys.dm_os_memory_clerks;
                                       """;

    public static async Task<MemoryClerkSnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(SnapshotSql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var clerks = await ReadTypes(reader, cancellationToken);

        var objects = await reader.NextResultAsync(cancellationToken) ? await ReadTypes(reader, cancellationToken) : [];

        var allocators = await reader.NextResultAsync(cancellationToken) ? await ReadTypes(reader, cancellationToken) : [];

        foreach (var (address, type) in allocators)
        {
            clerks.TryAdd(address, type);
        }

        return new MemoryClerkSnapshot(clerks, objects);
    }

    internal static ulong AddressOf(ReadOnlySpan<byte> bytes)
    {
        ulong address = 0;

        foreach (var value in bytes)
        {
            address = (address << 8) | value;
        }

        return address;
    }

    private static async Task<Dictionary<ulong, string>> ReadTypes(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var types = new Dictionary<ulong, string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetValue(0) is byte[] address && reader.GetValue(1) is string type)
            {
                types[AddressOf(address)] = type;
            }
        }

        return types;
    }
}
