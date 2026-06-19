using System.Data;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal abstract class VerificationService(IDatabaseService databaseService)
{
    public string LogFilename { get; set; } = string.Empty;

    private IDatabaseService DatabaseService { get; } = databaseService;

    protected void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(message);
        File.AppendAllText(LogFilename, $"SUCCESS: {message}{Environment.NewLine}");
    }

    protected void WriteMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;

        Console.WriteLine(message);

        File.AppendAllText(LogFilename, $"MESSAGE: {message}{Environment.NewLine}");
    }

    protected void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;

        Console.WriteLine(message);

        File.AppendAllText(LogFilename, $"ERROR: {message}{Environment.NewLine}");
    }

    protected async Task<DatabaseSource> CreateDatabase(string databaseName)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        var connection = ServerConnectionFactory.Create(config => config.ConnectionString = connectionString);

        var database = await DatabaseService.LoadAsync(databaseName, connection);

        return database;
    }

    protected string ConvertStringToSqlDbType(string? value, SqlDbType sqlDbType)
    {
        if (value == null || value == "[NULL]" || string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        switch (sqlDbType)
        {
            case SqlDbType.Int:
                return int.Parse(value).ToString();
            case SqlDbType.Float:
                return float.Parse(value).ToString("0.00");

            case SqlDbType.Bit:
            {
                if (bool.TryParse(value, out var result))
                {
                    return result.ToString();
                }

                return value == "1" ? "True" : "False";
            }
            case SqlDbType.SmallInt:
                return short.Parse(value).ToString();
            case SqlDbType.BigInt:
                return long.Parse(value).ToString();
            case SqlDbType.Real:
                return float.Parse(value).ToString("0.00");
            case SqlDbType.Money:
                return decimal.Parse(value.Replace("$", "")).ToString("0.00");
            case SqlDbType.SmallMoney:
                return decimal.Parse(value.Replace("$", "")).ToString("0.00");
            case SqlDbType.Decimal:
                return decimal.Parse(value).ToString("0.00");
            case SqlDbType.TinyInt:
                return byte.Parse(value).ToString();
            case SqlDbType.UniqueIdentifier:
                return Guid.Parse(value).ToString();
            case SqlDbType.Char:
            case SqlDbType.NChar:
            case SqlDbType.VarChar:
            case SqlDbType.NVarChar:
            case SqlDbType.Text:
            case SqlDbType.NText:
                return value;
            case SqlDbType.Binary:
            case SqlDbType.VarBinary:
            case SqlDbType.Image:
                return value;
            case SqlDbType.Date:
                return DateTime.Parse(value).ToString("yyyy-MM-dd");
            case SqlDbType.DateTime2:
            case SqlDbType.DateTimeOffset:
            case SqlDbType.SmallDateTime:
            case SqlDbType.DateTime:
                return DateTime.Parse(value).ToString();
            case SqlDbType.Time:
                return TimeSpan.Parse(value).ToString();
            case SqlDbType.Variant:
                return value;
            default:
                throw new ArgumentException($"Cannot convert to type: {sqlDbType}");
        }
    }
}