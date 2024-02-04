using System.Data;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using InternalsViewer.Internals.Tests.VerificationTool.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class TableVerificationService(ILogger<TableVerificationService> logger,
                                        ObjectService objectService,
                                        ObjectPageListService objectPageListService,
                                        IDatabaseLoader databaseLoader,
                                        IPageService pageService,
                                        IRecordService recordService)
{
    private ILogger<TableVerificationService> Logger { get; } = logger;

    private ObjectService ObjectService { get; } = objectService;

    private ObjectPageListService ObjectPageListService { get; } = objectPageListService;

    private IDatabaseLoader DatabaseLoader { get; } = databaseLoader;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public async Task VerifyTables()
    {
        var database = await CreateDatabase();

        var results = new List<VerificationResult>();

        var tables = await ObjectService.GetTables();

        foreach (var objectId in tables)
        {
            results.AddRange(await VerifyTable(objectId, database));
        }

        WriteSummary(results);
    }

    private void WriteSummary(List<VerificationResult> results)
    {
        WriteMessage($"Verification complete. {results.Count} result(s)");

        WriteSuccess($"{results.Sum(r => r.PassCount)} passed");
        WriteError($"{results.Sum(r => r.FailCount)} failed");
    }

    public async Task<List<VerificationResult>> VerifyTable(int objectId)
    {
        var database = await CreateDatabase();

        return await VerifyTable(objectId, database);
    }

    private async Task<List<VerificationResult>> VerifyTable(int objectId, DatabaseSource database)
    {
        var results = new List<VerificationResult>();

        WriteMessage($"Verifying table {objectId}");

        var pages = await ObjectPageListService.GetPages(objectId, null, Engine.Pages.Enums.PageType.Data);

        WriteMessage($"{pages.Count} page(s) found");

        foreach (var page in pages)
        {
            results.AddRange(await VerifyPage(page, database));
        }

        return results;
    }

    private async Task<List<VerificationResult>> VerifyPage(PageAddress page, DatabaseSource database)
    {
        var results = new List<VerificationResult>();

        var databaseVersions = await GetDatabaseRows(page);

        var internalsVersions = await GetInternalsRows(page, database);

        if (databaseVersions.Count != internalsVersions.Count)
        {
            WriteError($"Row count mismatch, Database version: {databaseVersions.Count} vs Internals {internalsVersions.Count}");
        }

        foreach (var databaseVersion in databaseVersions)
        {
            var result = new VerificationResult
            {
                PageAddress = page,
                Slot = databaseVersion.Row,
            };

            var internalsVersion = internalsVersions.FirstOrDefault(r => r.Slot == databaseVersion.Row);

            if (internalsVersion == null)
            {
                Logger.LogError($"Row {databaseVersion.Row} not found in Internals version");

                result.IsVerified = false;

                results.Add(result);

                continue;
            }

            foreach (var field in databaseVersion.Values)
            {
                var sanitizedField = field.Name.Replace(" (key)", string.Empty);

                var internalsField = internalsVersion.Fields.FirstOrDefault(f => f.Name == sanitizedField);

                if (internalsField == null)
                {
                    result.FailCount += 1;
                    WriteError($"{result.PageAddress}:{result.Slot} - Field {sanitizedField} not found in Internals version");

                    continue;
                }

                var databaseValue = ConvertStringToSqlDbType(field.Value, internalsField.ColumnStructure.DataType)?.Replace("''", "'") ?? "(null)";
                var internalsValue = ConvertStringToSqlDbType(internalsField.Value, internalsField.ColumnStructure.DataType) ?? "(null)";

                if (!internalsValue.Equals(databaseValue, StringComparison.OrdinalIgnoreCase))
                {
                    result.FailCount += 1;
                    WriteError($"{result.PageAddress}:{result.Slot} Field {field.Name} value mismatch" +
                               $" - Database: {field.Value ?? "(null)"} vs Internals: {internalsField.Value}");
                }
                else
                {
                    result.PassCount += 1;
                }
            }

            results.Add(result);
        }

        return results;
    }

    public string ConvertStringToSqlDbType(string? value, SqlDbType sqlDbType)
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

    private void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(message);
    }

    private void WriteMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;

        Console.WriteLine(message);
    }

    private void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;

        Console.WriteLine(message);
    }

    private async Task<DatabaseSource> CreateDatabase()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        var connection = ServerConnectionFactory.Create(config => config.ConnectionString = connectionString);

        var database = await DatabaseLoader.Load("AdventureWorks2022", connection);

        return database;
    }

    private async Task<List<DataRecord>> GetInternalsRows(PageAddress pageAddress, DatabaseSource database)
    {
        var page = await PageService.GetPage<DataPage>(database, pageAddress);

        if (page.IsPageCompressed)
        {
            return new List<DataRecord>();
        }

        try
        {
            var rows = RecordService.GetDataRecords(page);

            return rows;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);

            return new List<DataRecord>();
        }
    }

    private async Task<List<DatabaseTableRow>> GetDatabaseRows(PageAddress page)
    {
        var results = new List<DatabaseTableRow>();

        try
        {
            results.AddRange(await GetTableRows(page));
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);

            return results;
        }

        return results;
    }

    private async Task<IEnumerable<DatabaseTableRow>> GetTableRows(PageAddress page)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("Default");

        var connection = new SqlConnection(connectionString);

        var pageCommand = string.Format("DBCC PAGE('{0}', {1}, {2}, {3}) WITH TABLERESULTS",
            connection.Database,
            page.FileId,
            page.PageId,
            3);

        await connection.OpenAsync();
        await using var command = new SqlCommand(pageCommand, connection);

        command.CommandType = CommandType.Text;

        var results = new List<DatabaseTableRow>();


        var reader = await command.ExecuteReaderAsync();

        if (reader.HasRows)
        {
            while (reader.Read())
            {
                var parentObject = reader.GetString(0);
                var objectName = reader.GetString(1);
                var field = reader.GetString(2);
                var value = reader.GetString(3);

                if (!(parentObject.StartsWith("Slot") && objectName.StartsWith("Slot")) || field == "KeyHashValue")
                {
                    continue;
                }

                var slot = int.Parse(parentObject.Split(" ")[1]);

                var record = results.FirstOrDefault(r => r.Row == slot);

                if (record == null)
                {
                    results.Add(new DatabaseTableRow
                    {
                        FileId = page.FileId,
                        PageId = page.PageId,
                        Row = (short)slot,
                        Values = new List<DatabaseField>
                        {
                            new() { Name = field, Value = value }
                        }
                    });
                }
                else
                {
                    record.Values.Add(new DatabaseField { Name = field, Value = value });
                }
            }

            reader.Close();
        }

        return results;
    }
}