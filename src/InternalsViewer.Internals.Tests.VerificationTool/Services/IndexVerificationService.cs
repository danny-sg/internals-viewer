using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using InternalsViewer.Internals.Tests.VerificationTool.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

internal class IndexVerificationService(ILogger<IndexVerificationService> logger,
                                        ObjectService objectService,
                                        ObjectPageListService objectPageListService,
                                        IDatabaseService databaseService,
                                        IPageService pageService,
                                        IRecordService recordService): VerificationService(databaseService)
{
    private ILogger<IndexVerificationService> Logger { get; } = logger;

    private ObjectService ObjectService { get; } = objectService;

    private ObjectPageListService ObjectPageListService { get; } = objectPageListService;

    private IDatabaseService DatabaseService { get; } = databaseService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public async Task VerifyIndexes(string databaseName)
    {
        var database = await CreateDatabase(databaseName);

        var results = new List<VerificationResult>();

        var indexes = await ObjectService.GetIndexes(databaseName);

        foreach (var index in indexes)
        {

            WriteMessage($"Verifying index {index.ObjectId}.{index.IndexId} - {index.Name} (Unique: {index.isUnique}, PrimaryKey: {index.isPrimaryKey}, UniqueConstraint: {index.isUniqueConstraint})");
            results.AddRange(await VerifyIndex(index.ObjectId, index.IndexId, database));
        }

        WriteSummary(results);
    }

    private void WriteSummary(List<VerificationResult> results)
    {
        WriteMessage($"Verification complete. {results.Count} result(s)");

        WriteSuccess($"{results.Sum(r => r.PassCount)} passed");
        WriteError($"{results.Sum(r => r.FailCount)} failed");
    }

    public async Task<List<VerificationResult>> VerifyIndex(string databaseName, int objectId, int indexId)
    {
        var database = await CreateDatabase(databaseName);

        return await VerifyIndex(objectId, indexId, database);
    }

    private async Task<List<VerificationResult>> VerifyIndex(int objectId, int indexId, DatabaseSource database)
    {
        var results = new List<VerificationResult>();


        var pages = await ObjectPageListService.GetPages(database.Name, objectId, indexId, Engine.Pages.Enums.PageType.Index);

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

        var databaseVersions = await GetDatabaseRows(database.Name, page);

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
                }
                else
                {
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
            }

            if (databaseVersion.Rid != null && databaseVersion.Rid != internalsVersion.Rid)
            {
                result.FailCount += 1;

                WriteError($"{result.PageAddress}:{result.Slot} - RID mismatch - {databaseVersion.Rid} vs {internalsVersion.Rid}");
            }

            results.Add(result);
        }

        return results;
    }

    private async Task<List<IIndexRecord>> GetInternalsRows(PageAddress pageAddress, DatabaseSource database)
    {
        var page = await PageService.GetPage<IndexPage>(database, pageAddress);

        var rows = RecordService.GetIndexRecords(page);

        return rows.ToList();
    }

    private async Task<List<DatabaseIndexRow>> GetDatabaseRows(string databaseName, PageAddress page)
    {
        var results = new List<DatabaseIndexRow>();

        try
        {
            results.AddRange(await GetIndexRows(databaseName, page));
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);

            return results;
        }

        return results;
    }

    private async Task<IEnumerable<DatabaseIndexRow>> GetIndexRows(string databaseName, PageAddress page)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var connection = new SqlConnection(connectionString);

        var pageCommand = string.Format("DBCC PAGE('{0}', {1}, {2}, {3})",
                                        connection.Database,
                                        page.FileId,
                                        page.PageId,
                                        3);

        await connection.OpenAsync();
        await using var command = new SqlCommand(pageCommand, connection);

        command.CommandType = CommandType.Text;

        var results = new List<DatabaseIndexRow>();


        var reader = await command.ExecuteReaderAsync();

        if (reader.HasRows)
        {
            var fields = Enumerable.Range(0, reader.FieldCount)
                                   .Select(reader.GetName)
                                   .ToList();

            RowIdentifier? rid = null;

            while (reader.Read())
            {
                var fileId = reader.GetInt16(0);
                var pageId = reader.GetInt32(1);
                var row = reader.GetInt16(2);
                var level = reader.GetInt16(3);

                var offset = 4;

                short childFileId = 0;
                int childPageId = 0;

                if (fields.Contains("ChildFileId"))
                {
                    childFileId = reader.GetInt16(offset);
                    offset++;
                }

                if (fields.Contains("ChildPageId"))
                {
                    childPageId = reader.GetInt32(offset);
                    offset++;
                }

                var values = new List<DatabaseField>();

                for (var i = offset; i < reader.FieldCount - 2; i++)
                {
                    var name = reader.GetName(i);

                    if (name == "HEAP RID")
                    {
                        var ridData = new byte[RowIdentifier.Size];

                        reader.GetBytes(i, 0, ridData, 0, RowIdentifier.Size);

                        rid = new RowIdentifier(ridData);
                    }
                    else
                    {
                        var value = reader.GetValue(i);

                        var dataType = reader.GetDataTypeName(i);

                        switch (dataType, reader.IsDBNull(i))
                        {
                            case (_, true):
                                values.Add(new DatabaseField { Name = name, Value = string.Empty });
                                break;
                            case ("binary", _):
                            case ("varbinary", _):
                                values.Add(new DatabaseField { Name = name, Value = "0x" + StringHelpers.ToHexString((byte[])value) });
                                break;
                            case ("date", _):
                                values.Add(new DatabaseField { Name = name, Value = $"{value:dd/MM/yyyy}" });
                                break;
                            case ("datetime", _):
                                values.Add(new DatabaseField { Name = name, Value = $"{value:dd/MM/yyyy HH:mm:ss}" });
                                break;
                            case ("dateTime2", _):
                                values.Add(new DatabaseField { Name = name, Value = $"{value:dd/MM/yyyy HH:mm:ss.fff}" });
                                break;
                            default:
                                values.Add(new DatabaseField { Name = name, Value = value?.ToString() });
                                break;
                        }
                    }
                }

                var keyHash = reader.GetValue(reader.FieldCount - 2);
                var rowSize = reader.GetInt16(reader.FieldCount - 1);

                results.Add(new DatabaseIndexRow
                {
                    FileId = fileId,
                    PageId = pageId,
                    Row = row,
                    Level = level,
                    ChildFileId = childFileId,
                    ChildPageId = childPageId,
                    Values = values,
                    RowSize = rowSize,
                    Rid = rid
                });
            }

            reader.Close();
        }

        return results;
    }
}