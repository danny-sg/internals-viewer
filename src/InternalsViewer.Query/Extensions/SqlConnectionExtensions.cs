using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Extensions;

internal static class SqlConnectionExtensions
{
    internal static async Task<T?> ExecuteScalar<T>(this SqlConnection connection,
                                                    string sql,
                                                    CancellationToken cancellationToken,
                                                    ILogger? logger = null)
    {
        logger?.LogDebug("Executing SQL: {Command}", sql);

        var result = await new SqlCommand(sql, connection).ExecuteScalarAsync(cancellationToken);

        return (T?)result;
    }

    internal static async Task<int> ExecuteSql(this SqlConnection connection, 
                                               string sql, 
                                               CancellationToken cancellationToken,
                                               ILogger? logger = null)
    {
        logger?.LogDebug("Executing SQL: {Command}", sql);

        using var command = new SqlCommand(sql, connection);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
