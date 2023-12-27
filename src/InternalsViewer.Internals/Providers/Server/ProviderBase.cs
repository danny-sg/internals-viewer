using System.Data;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Providers.Server;

public abstract class ProviderBase(CurrentConnection connection)
{
    public CurrentConnection Connection { get; } = connection;

    protected async Task<T?> GetScalar<T>(string commandText, SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(Connection.ConnectionString);

        var command = new SqlCommand(commandText, connection);

        command.CommandType = CommandType.Text;

        command.Parameters.AddRange(parameters);

        await command.Connection.OpenAsync();

        await connection.ChangeDatabaseAsync(Connection.DatabaseName);

        var result = await command.ExecuteScalarAsync();

        if (result != null)
        {
            return (T)result;
        }

        return default;
    }
}