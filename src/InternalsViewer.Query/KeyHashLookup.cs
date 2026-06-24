using InternalsViewer.Internals.Engine.Address;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query;

public sealed class KeyHashLookup(ILogger<KeyHashLookup> logger)
{
    private const int BatchSize = 2000;

    public ILogger<KeyHashLookup> Logger { get; } = logger;

    public static async Task<Dictionary<string, RowIdentifier>> GetKeyHashRowIdentifiers(string objectName,
        List<string> hashes,
        string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var result = new Dictionary<string, RowIdentifier>();

        for (var offset = 0; offset < hashes.Count; offset += BatchSize)
        {
            var batch = hashes.Skip(offset).Take(BatchSize).ToList();

            var paramNames = batch.Select((_, i) => $"@h{i}").ToList();

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

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var rowIdentifier = reader.GetSqlBinary(0);
                var lockHash = reader.GetString(1);

                result[lockHash] = new RowIdentifier(rowIdentifier.Value);
            }
        }

        return result;
    }
}