using Microsoft.Data.SqlClient;

namespace InternalsViewer.Query.FullTrace.Symbols;

public static class LoadedModuleReader
{
    private const string Sql = "SELECT name FROM sys.dm_os_loaded_modules";

    public static async Task<IReadOnlyList<string>> GetPathsAsync(string connectionString,
                                                                  IReadOnlyCollection<string> fileNames,
                                                                  CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var paths = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(0);

            if (fileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return paths;
    }
}
