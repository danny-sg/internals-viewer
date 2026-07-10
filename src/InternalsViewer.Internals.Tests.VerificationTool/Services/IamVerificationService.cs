using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using InternalsViewer.Internals.Tests.VerificationTool.Models;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

/// <summary>
/// Cross-checks the app's own page-to-allocation-unit tracking (DatabaseSource.FindPageAllocationUnit,
/// backed by each AllocationUnit's IamChain) against DBCC IND - SQL Server's own list of pages it
/// considers part of a table/index - and against sys.dm_db_database_page_allocations, which additionally
/// confirms the exact AllocationUnitId a page belongs to (DBCC IND only reports object/index id, so it
/// can't distinguish between the multiple allocation units - e.g. IN_ROW_DATA vs LOB_DATA - that can
/// share the same object/index id).
/// </summary>
internal class IamVerificationService(ObjectService objectService,
                                      IDatabaseService databaseService) : VerificationService(databaseService)
{
    private ObjectService ObjectService { get; } = objectService;

    public async Task VerifyAllIam(string databaseName)
    {
        var database = await CreateDatabase(databaseName);

        var indexes = await ObjectService.GetIndexesAndHeaps(databaseName);

        var results = new List<VerificationResult>();

        foreach (var index in indexes)
        {
            WriteMessage($"Verifying allocation for {index.ObjectId}.{index.IndexId} - {index.Name}");

            results.AddRange(await VerifyIam(databaseName, index.ObjectId.ToString(), index.ObjectId, index.IndexId, database));
        }

        WriteSummary(results);
    }

    public async Task VerifyIam(string databaseName, string tableName, int indexId)
    {
        var database = await CreateDatabase(databaseName);

        var results = await VerifyIam(databaseName, $"'{tableName}'", null, indexId, database);

        WriteSummary(results);
    }

    private async Task<List<VerificationResult>> VerifyIam(string databaseName,
                                                           string tableSelector,
                                                           int? expectedObjectId,
                                                           int indexId,
                                                           DatabaseSource database)
    {
        var results = new List<VerificationResult>();

        var indPages = await GetIndPages(databaseName, tableSelector, indexId);

        WriteMessage($"{indPages.Count} page(s) reported by DBCC IND for {tableSelector}, index {indexId}");

        if (indPages.Count == 0)
        {
            WriteError("DBCC IND returned no pages - check the table name/index id");

            return results;
        }

        var objectId = expectedObjectId ?? indPages[0].ObjectId;

        foreach (var indPage in indPages)
        {
            var result = new VerificationResult { PageAddress = indPage.PageAddress };

            var allocationUnit = database.FindPageAllocationUnit(indPage.PageAddress);

            if (allocationUnit is null)
            {
                result.FailCount = 1;

                WriteError($"{indPage.PageAddress} (PageType {indPage.PageType}) - not tracked by any allocation unit");
            }
            else if (allocationUnit.ObjectId != objectId || allocationUnit.IndexId != indPage.IndexId)
            {
                result.FailCount = 1;

                WriteError($"{indPage.PageAddress} (PageType {indPage.PageType}) - tracked as " +
                           $"{allocationUnit.SchemaName}.{allocationUnit.TableName}.{allocationUnit.IndexName} " +
                           $"(ObjectId {allocationUnit.ObjectId}, IndexId {allocationUnit.IndexId}) instead of " +
                           $"ObjectId {objectId}, IndexId {indPage.IndexId}");
            }
            else
            {
                result.PassCount = 1;
            }

            results.Add(result);
        }

        results.AddRange(VerifyReverseAllocation(database, objectId, indexId, indPages));

        results.AddRange(await VerifyAllocationUnitIds(databaseName, objectId, indexId, database));

        return results;
    }

    private async Task<List<VerificationResult>> VerifyAllocationUnitIds(string databaseName,
                                                                          int objectId,
                                                                          int indexId,
                                                                          DatabaseSource database)
    {
        var results = new List<VerificationResult>();

        var pageAllocations = await GetPageAllocations(databaseName, objectId, indexId);

        WriteMessage($"{pageAllocations.Count} page(s) reported by sys.dm_db_database_page_allocations for " +
                     $"ObjectId {objectId}, IndexId {indexId}");

        foreach (var pageAllocation in pageAllocations)
        {
            var result = new VerificationResult { PageAddress = pageAllocation.PageAddress };

            var allocationUnit = database.FindPageAllocationUnit(pageAllocation.PageAddress);

            if (allocationUnit is null)
            {
                result.FailCount = 1;

                WriteError($"{pageAllocation.PageAddress} (PageType {pageAllocation.PageType}) - not tracked by any allocation unit " +
                           $"(expected AllocationUnitId {pageAllocation.AllocationUnitId})");
            }
            else if (allocationUnit.AllocationUnitId != pageAllocation.AllocationUnitId)
            {
                result.FailCount = 1;

                WriteError($"{pageAllocation.PageAddress} (PageType {pageAllocation.PageType}) - tracked under AllocationUnitId " +
                           $"{allocationUnit.AllocationUnitId} ({allocationUnit.SchemaName}.{allocationUnit.TableName}." +
                           $"{allocationUnit.IndexName}) instead of AllocationUnitId {pageAllocation.AllocationUnitId} " +
                           "reported by sys.dm_db_database_page_allocations");
            }
            else
            {
                result.PassCount = 1;
            }

            results.Add(result);
        }

        return results;
    }

    private List<VerificationResult> VerifyReverseAllocation(DatabaseSource database,
                                                              int objectId,
                                                              int indexId,
                                                              List<DatabaseIndPageRow> indPages)
    {
        var results = new List<VerificationResult>();

        var allocationUnits = database.AllocationUnits
                                       .Values
                                       .Where(a => a.ObjectId == objectId && a.IndexId == indexId)
                                       .ToList();

        if (allocationUnits.Count == 0)
        {
            return results;
        }

        var databasePages = indPages.Select(p => p.PageAddress).ToHashSet();

        foreach (var allocationUnit in allocationUnits)
        {
            foreach (var fileGroup in databasePages.GroupBy(p => p.FileId))
            {
                var fileId = fileGroup.Key;

                var minExtent = fileGroup.Min(p => p.PageId / 8);
                var maxExtent = fileGroup.Max(p => p.PageId / 8);

                for (var extent = minExtent; extent <= maxExtent; extent++)
                {
                    if (!allocationUnit.IamChain.IsExtentAllocated(extent, fileId, false))
                    {
                        continue;
                    }

                    for (var i = 0; i < 8; i++)
                    {
                        var pageAddress = new PageAddress(fileId, (extent * 8) + i);

                        if (databasePages.Contains(pageAddress))
                        {
                            continue;
                        }

                        results.Add(new VerificationResult { PageAddress = pageAddress, FailCount = 1 });

                        WriteError($"{pageAddress} - allocated to {allocationUnit.SchemaName}.{allocationUnit.TableName}." +
                                   $"{allocationUnit.IndexName} according to the app's IamChain but not reported by DBCC IND");
                    }
                }
            }

            foreach (var singlePage in allocationUnit.IamChain.SinglePageSlots.Where(s => s != PageAddress.Empty))
            {
                if (databasePages.Contains(singlePage))
                {
                    continue;
                }

                results.Add(new VerificationResult { PageAddress = singlePage, FailCount = 1 });

                WriteError($"{singlePage} - single page slot allocated to {allocationUnit.SchemaName}.{allocationUnit.TableName}." +
                           $"{allocationUnit.IndexName} but not reported by DBCC IND");
            }
        }

        return results;
    }

    private void WriteSummary(List<VerificationResult> results)
    {
        WriteMessage($"Verification complete. {results.Count} page(s) checked");

        WriteSuccess($"{results.Sum(r => r.PassCount)} matched");
        WriteError($"{results.Sum(r => r.FailCount)} mismatched");
    }

    private async Task<List<DatabaseIndPageRow>> GetIndPages(string databaseName, string tableSelector, int indexId)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var connection = new SqlConnection(connectionString);

        var indCommand = $"DBCC IND ('{databaseName}', {tableSelector}, {indexId})";

        await connection.OpenAsync();

        await using var command = new SqlCommand(indCommand, connection);

        command.CommandType = CommandType.Text;

        var results = new List<DatabaseIndPageRow>();

        var reader = await command.ExecuteReaderAsync();

        if (reader.HasRows)
        {
            var fileIdOrdinal = reader.GetOrdinal("PageFID");
            var pageIdOrdinal = reader.GetOrdinal("PagePID");
            var iamFileIdOrdinal = reader.GetOrdinal("IAMFID");
            var iamPageIdOrdinal = reader.GetOrdinal("IAMPID");
            var objectIdOrdinal = reader.GetOrdinal("ObjectID");
            var indexIdOrdinal = reader.GetOrdinal("IndexID");
            var pageTypeOrdinal = reader.GetOrdinal("PageType");

            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(fileIdOrdinal) || reader.IsDBNull(pageIdOrdinal))
                {
                    continue;
                }

                results.Add(new DatabaseIndPageRow
                {
                    PageAddress = new PageAddress(GetInt16(reader, fileIdOrdinal), GetInt32(reader, pageIdOrdinal)),
                    ObjectId = GetInt32(reader, objectIdOrdinal),
                    IndexId = GetInt32(reader, indexIdOrdinal),
                    PageType = (byte)GetInt32(reader, pageTypeOrdinal),
                    IamPage = new PageAddress(GetInt16(reader, iamFileIdOrdinal), GetInt32(reader, iamPageIdOrdinal))
                });
            }
        }

        reader.Close();

        return results;
    }

    private async Task<List<DatabasePageAllocationRow>> GetPageAllocations(string databaseName, int objectId, int indexId)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var connection = new SqlConnection(connectionString);

        var sql = @"
            SELECT allocated_page_file_id, allocated_page_page_id, allocation_unit_id, object_id, index_id, page_type
            FROM   sys.dm_db_database_page_allocations(DB_ID(), @ObjectId, @IndexId, NULL, 'DETAILED')
            WHERE  is_allocated = 1";

        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@ObjectId", objectId);
        command.Parameters.AddWithValue("@IndexId", indexId);

        var results = new List<DatabasePageAllocationRow>();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new DatabasePageAllocationRow
            {
                PageAddress = new PageAddress(GetInt16(reader, 0), GetInt32(reader, 1)),
                AllocationUnitId = GetInt64(reader, 2),
                ObjectId = GetInt32(reader, 3),
                IndexId = GetInt32(reader, 4),
                PageType = (byte)GetInt32(reader, 5)
            });
        }

        reader.Close();

        return results;
    }

    // DBCC IND's column types aren't consistent across SQL Server versions (some report as smallint,
    // some as int), so read via the boxed value and convert rather than assuming a specific reader
    // method - GetInt32/GetInt16 throw InvalidCastException the moment the actual type doesn't match.
    private static short GetInt16(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? (short)0 : Convert.ToInt16(reader.GetValue(ordinal));

    private static int GetInt32(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));

    private static long GetInt64(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
}
