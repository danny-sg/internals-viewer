using InternalsViewer.Query.Debugging.Exceptions;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// SQL Server Process Id/Machine Name
/// </summary>
public sealed record SqlServerProcess(int ProcessId, string MachineName)
{
    private const string Sql = "SELECT CAST(SERVERPROPERTY('ProcessID') AS int), CAST(SERVERPROPERTY('MachineName') AS nvarchar(128))";

    public bool IsLocal => string.Equals(MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    public static async Task<SqlServerProcess> GetAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            throw new DebuggerException("SQL Server did not report its process id");
        }

        return new SqlServerProcess(reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
    }
}
