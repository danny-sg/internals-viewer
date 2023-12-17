using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Readers.Internals;

namespace InternalsViewer.Internals.Services.Loaders;

/// <summary>
/// Service responsible for building metadata for a database
/// </summary>
/// <param name="logger"></param>
/// <param name="tableReader"></param>
public class MetadataService(ILogger<MetadataService> logger, TableReader tableReader)
{
    public ILogger<MetadataService> Logger { get; } = logger;

    public TableReader TableReader { get; } = tableReader;

    public List<InternalAllocationUnit> AllocationUnits { get; set; } = new();

    public List<InternalRowSet> RowSets { get; set; } = new();

    public List<InternalObject> Objects { get; set; } = new();

    public List<InternalColumn> Columns { get; set; } = new();

    /// <summary>
    /// Builds the metadata for database from the raw page data
    /// </summary>
    /// <remarks>
    /// SQL Server has a set of base tables that store internal metadata about the database. All sys/DMVs etc are derived from these 
    /// tables.
    /// 
    /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/system-tables/system-base-tables"/>
    /// 
    /// The first table to read is sys.sysallocunits. This defines the first page location for each allocation unit in the database. 
    /// 
    /// The first page address for this is stored in the boot page (1:9) in the field dbi_firstSysIndexes 
    /// <see cref="Pages.BootPage.FirstAllocationUnitsPage"/>
    /// 
    /// Once that is loaded in the other tables can be found via their allocation unit id, derived from Object Id + Index Id. 
    /// 
    /// Object Id/Index Id are constant for the tables - see sys.objects and <see cref="InternalObjectIdConstants"/>."
    /// 
    /// <see cref="InternalAllocationUnit"/>
    /// sys.sysallocunits 
    ///     <see cref="InternalRowSet"/>
    ///     --> sys.sysrowsets
    ///     <see cref="InternalObject"/>
    ///     --> sys.sysrscols 
    ///     <see cref="InternalColumn"/>
    ///     --> sys.sysrscols
    /// </remarks>
    public async Task BuildMetadata(Database database)
    {
        Logger.LogDebug("Database: {DatabaseName} - Building metadata", database.Name);

        AllocationUnits = await GetAllocationUnits(database);

        RowSets = await GetRowSets(database);

        Objects = await GetObjects(database);

        Columns = await GetColumns(database);
    }

    private async Task<List<InternalObject>> GetObjects(Database database)
    {
        Logger.LogDebug("Getting Objects (sys.sysschobjs) using fixed Object Id/Index Id");

        var firstPage = GetFirstPage(InternalObjectIdConstants.ObjectsId);

        var records = await TableReader.Read(database,
                                             firstPage,
                                             InternalObjectStructure.GetStructure(-1));

        Logger.LogDebug("Objects: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalObjectLoader.Load).ToList();

        Logger.LogDebug("Objects: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalRowSet>> GetRowSets(Database database)
    {
        Logger.LogDebug("Getting Row Sets (sys.sysrowsets) using fixed Object Id/Index Id");

        var firstPage = GetFirstPage(InternalObjectIdConstants.RowSetId);

        var records = await TableReader.Read(database,
                                             firstPage,
                                             InternalRowSetStructure.GetStructure(-1));

        Logger.LogDebug("Row Sets: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalRowSetLoader.Load).ToList();

        Logger.LogDebug("Row Sets: {Count} records parsed.", rows.Count);

        return rows;
    }


    private async Task<List<InternalColumn>> GetColumns(Database database)
    {
        Logger.LogDebug("Getting Columns (sys.sysrscols) using fixed Object Id/Index Id");

        var firstPage = GetFirstPage(InternalObjectIdConstants.ColumnsId);

        var records = await TableReader.Read(database,
                                             firstPage,
                                             InternalColumnStructure.GetStructure(-1));

        Logger.LogDebug("Columns: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalColumnLoader.Load).ToList();

        Logger.LogDebug("Columns: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalAllocationUnit>> GetAllocationUnits(Database database)
    {
        var (objectId, indexId) = InternalObjectIdConstants.ObjectsId;

        var pageAddress = database.BootPage.FirstAllocationUnitsPage;

        Logger.LogDebug("Getting Allocation Units (sys.sysallocunits) using first page specified in Boot Page (1:9): {PageAddress}",
                        pageAddress);

        var id = IdHelpers.GetAllocationUnitId(objectId, indexId);

        var records = await TableReader.Read(database,
                                             database.BootPage.FirstAllocationUnitsPage,
                                             InternalAllocationUnitStructure.GetStructure(id));

        Logger.LogDebug("Allocation Units: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalAllocationUnitLoader.Load).ToList();

        Logger.LogDebug("Allocation Units: {Count} records parsed.", rows.Count);

        return rows;
    }

    private PageAddress GetFirstPage((int ObjectId, int IndexId) id)
    {
        var (objectId, indexId) = id;

        var allocationUnitId = IdHelpers.GetAllocationUnitId(objectId, indexId);

        var firstPage = AllocationUnits.FirstOrDefault(f => f.AllocationUnitId == allocationUnitId)?.FirstPage?.ToPageAddress();

        Logger.LogDebug("Getting first page for Object Id: {ObjectId}, Index Id: {IndexId} (Allocation Unit Id: {AllocationUnitId}) " +
                        "=> {FirstPage}",
                        objectId,
                        indexId,
                        allocationUnitId,
                        firstPage?.ToString() ?? "NOT FOUND");

        if (firstPage == null)
        {
            throw new InvalidOperationException($"Allocation unit not found - Object Id: {objectId}, Index Id: {indexId} " +
                                                $"=> {allocationUnitId}");
        }

        return firstPage.Value;
    }
}