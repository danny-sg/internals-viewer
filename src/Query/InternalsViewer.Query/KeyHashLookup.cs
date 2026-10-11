using InternalsViewer.Internals.Engine.Address;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query;

public sealed class KeyHashLookup(ILogger<KeyHashLookup> logger)
{
    private const int BatchSize = 2000;

    public ILogger<KeyHashLookup> Logger { get; } = logger;

    public static async Task<Dictionary<string, RowIdentifier>>
        GetKeyHashRowIdentifiers(string schemaName,
                                 string tableName,
                                 List<string> hashes,
                                 string connectionString,
                                 CancellationToken cancellationToken)
    {
        // The names come from the traced database's catalog, so they can legally need quoting (spaces, reserved words,
        // brackets) — unquoted, [Order Details] parses as [Order] AS [Details] and the whole trace fails.
        var objectName = $"{QuoteName(schemaName)}.{QuoteName(tableName)}";

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        var result = new Dictionary<string, RowIdentifier>();

        for (var offset = 0; offset < hashes.Count; offset += BatchSize)
        {
            var batch = hashes.Skip(offset).Take(BatchSize).ToList();

            var paramNames = batch.Select((_, i) => $"@h{i}").ToList();

            // TODO: %%lockres%% is INDEX-SPECIFIC (the hash is over the accessed index's key columns), and WHERE
            // %%lockres%% isn't sargable so this clustered-scans and computes CLUSTERED key hashes. That resolves
            // clustered/heap key locks (the common escalation case) but NOT a lock taken on a nonclustered index —
            // its hashes never match. Fix: the caller (GetEventKeyAddresses) already groups by AllocationUnit, which
            // IS one specific index, so pass its index name through and force it — FROM {objectName} WITH
            // (INDEX([indexName])) — for a non-clustered rowset. (Note: %%physloc%% under a forced NC scan is the
            // index entry's location, not the base row's.)
            var sql = $@"
SELECT %%physloc%% AS RowIdentifier
      ,%%lockres%% AS [LockHash]
FROM   {objectName}
WHERE  %%lockres%% IN ({string.Join(", ", paramNames)})";

            await using var command = new SqlCommand(sql, connection);

            for (var i = 0; i < batch.Count; i++)
            {
                command.Parameters.AddWithValue($"@h{i}", batch[i]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var rowIdentifier = reader.GetSqlBinary(0);
                var lockHash = reader.GetString(1);

                result[lockHash] = new RowIdentifier(rowIdentifier.Value);
            }
        }

        return result;
    }

    /// <summary>
    /// Brackets an identifier with QUOTENAME semantics (embedded closing brackets doubled)
    /// </summary>
    private static string QuoteName(string name) => $"[{name.Replace("]", "]]")}]";
}