using InternalsViewer.Query.Results;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace InternalsViewer.Query.Extensions;

internal static class SqlConnectionExtensions
{
    internal static async Task<T?> ExecuteScalar<T>(this SqlConnection connection,
                                                    string sql,
                                                    CancellationToken cancellationToken,
                                                    ILogger? logger = null)
    {
        var commandStart = Stopwatch.GetTimestamp();

        logger?.LogDebug("Executing SQL: {Command}", sql);

        var result = await new SqlCommand(sql, connection).ExecuteScalarAsync(cancellationToken);

        logger?.LogDebug("Command executed in {Duration}", Stopwatch.GetElapsedTime(commandStart));

        return (T?)result;
    }

    internal static async Task<int> ExecuteSql(this SqlConnection connection, 
                                               string sql, 
                                               CancellationToken cancellationToken,
                                               ILogger? logger = null)
    {
        var commandStart = Stopwatch.GetTimestamp();

        logger?.LogDebug("Executing SQL: {Command}", sql);

        await using var command = new SqlCommand(sql, connection);

        var result = await command.ExecuteNonQueryAsync(cancellationToken);

        logger?.LogDebug("Command executed in {Duration}", Stopwatch.GetElapsedTime(commandStart));

        return result;
    }


    internal static List<ResultColumn> GetResultColumns(this SqlDataReader reader)
    {
        var schemaTable = reader.GetSchemaTable();

        var columns = new List<ResultColumn>(reader.FieldCount);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var typeName = reader.GetDataTypeName(i);
            var clrType = reader.GetFieldType(i) ?? typeof(object);
            var nullable = schemaTable?.Rows[i]["AllowDBNull"] is true;

            columns.Add(new ResultColumn(i, name, typeName, clrType, nullable));
        }

        return columns;
    }
}
