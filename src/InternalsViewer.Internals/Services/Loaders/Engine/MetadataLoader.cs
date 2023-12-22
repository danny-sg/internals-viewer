using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Services.Loaders.Engine;

/// <summary>
/// Service responsible for loading metadata for a database
/// </summary>
public class MetadataLoader(ILogger<MetadataLoader> logger, ITableReader tableReader)
    : IMetadataLoader
{
    public ILogger<MetadataLoader> Logger { get; } = logger;

    public ITableReader TableReader { get; } = tableReader;

    /// <summary>
    /// Builds the metadata for database from the raw page data
    /// </summary>
    /// <remarks>
    /// SQL Server has a set of base tables that store internal metadata about the database. All sys/DMVs etc are derived from these 
    /// tables.
    /// 
    /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/system-tables/system-base-tables"/> 
    /// (not all tables are mentioned in the article)
    /// 
    /// The first table to read is sys.sysallocunits. This defines the first page location for each allocation unit in the database. 
    /// 
    /// The first page address for this is stored in the boot page (1:9) in the field dbi_firstSysIndexes 
    /// <see cref="BootPage.FirstAllocationUnitsPage"/>
    /// 
    /// Once that is loaded in the other tables can be found via their allocation unit id, derived from Object Id + Index Id. 
    /// 
    /// Object Id/Index Id are constant for the tables - see sys.objects and <see cref="InternalTableConstants"/>."
    /// 
    /// <see cref="InternalAllocationUnit"/>
    /// sys.sysallocunits 
    ///     <see cref="InternalRowSet"/>
    ///     --> sys.sysrowsets
    ///     <see cref="InternalObject"/>
    ///     --> sys.sysrscols 
    ///     <see cref="InternalColumn"/>
    ///     --> sys.sysrscols
    ///     <see cref="InternalEntityObject"/>
    ///     --> sys.sysclsobjs
    ///     <see cref="InternalIndex"/>
    ///     --> sys.sysidxstats
    ///     <see cref="InternalFile"/>
    ///     --> sys.sysprufiles
    /// </remarks>
    public async Task<InternalMetadata> Load(DatabaseDetail database)
    {
        var result = new InternalMetadata();

        Logger.LogDebug("Database: {DatabaseName} - Building metadata", database.Name);

        result.AllocationUnits = await GetAllocationUnits(database);

        var rowSetsFirstPage = GetFirstPage(InternalTableConstants.RowSetId, result.AllocationUnits);

        result.RowSets = await GetRowSets(rowSetsFirstPage, database);

        var objectsFirstPage = GetFirstPage(InternalTableConstants.ObjectsId, result.AllocationUnits);

        result.Objects = await GetObjects(objectsFirstPage, database);

        var columnsFirstPage = GetFirstPage(InternalTableConstants.ColumnsId, result.AllocationUnits);

        result.Columns = await GetColumns(columnsFirstPage, database);

        var entitiesFirstPage = GetFirstPage(InternalTableConstants.EntitiesId, result.AllocationUnits);

        result.Entities = await GetEntities(entitiesFirstPage, database);

        var indexesFirstPage = GetFirstPage(InternalTableConstants.IndexesId, result.AllocationUnits);

        result.Indexes = await GetIndexes(indexesFirstPage, database);

        var filesFirstPage = GetFirstPage(InternalTableConstants.FilesId, result.AllocationUnits);

        result.Files = await GetFiles(filesFirstPage, database);

        return result;
    }

    private async Task<List<InternalFile>> GetFiles(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Objects (sys.sysschobjs) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalFileStructure.GetStructure(-1));

        Logger.LogDebug("Objects: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalFileLoader.Load).ToList();

        Logger.LogDebug("Objects: {Count} records parsed.", rows.Count);

        return rows;

    }

    private async Task<List<InternalEntityObject>> GetEntities(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Objects (sys.sysschobjs) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalEntityObjectStructure.GetStructure(-1));

        Logger.LogDebug("Objects: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalEntityObjectLoader.Load).ToList();

        Logger.LogDebug("Objects: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalObject>> GetObjects(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Objects (sys.sysschobjs) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalObjectStructure.GetStructure(-1));

        Logger.LogDebug("Objects: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalObjectLoader.Load).ToList();

        Logger.LogDebug("Objects: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalIndex>> GetIndexes(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Indexes (sys.sysidxstats) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalIndexStructure.GetStructure(-1));

        Logger.LogDebug("Objects: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalIndexLoader.Load).ToList();

        Logger.LogDebug("Objects: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalRowSet>> GetRowSets(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Row Sets (sys.sysrowsets) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalRowSetStructure.GetStructure(-1));

        Logger.LogDebug("Row Sets: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalRowSetLoader.Load).ToList();

        Logger.LogDebug("Row Sets: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalColumn>> GetColumns(PageAddress pageAddress, DatabaseDetail database)
    {
        Logger.LogDebug("Getting Columns (sys.sysrscols) using fixed Object Id/Index Id");

        var records = await TableReader.Read(database,
                                             pageAddress,
                                             InternalColumnStructure.GetStructure(-1));

        Logger.LogDebug("Columns: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalColumnLoader.Load).ToList();

        Logger.LogDebug("Columns: {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalAllocationUnit>> GetAllocationUnits(DatabaseDetail databaseDetail)
    {
        var (objectId, indexId) = InternalTableConstants.ObjectsId;

        var pageAddress = databaseDetail.BootPage.FirstAllocationUnitsPage;

        Logger.LogDebug("Getting Allocation Units (sys.sysallocunits) using first page specified in Boot Page (1:9): {PageAddress}",
                        pageAddress);

        var id = IdHelpers.GetAllocationUnitId(objectId, indexId);

        var records = await TableReader.Read(databaseDetail,
                                             databaseDetail.BootPage.FirstAllocationUnitsPage,
                                             InternalAllocationUnitStructure.GetStructure(id));

        Logger.LogDebug("Allocation Units: {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalAllocationUnitLoader.Load).ToList();

        Logger.LogDebug("Allocation Units: {Count} records parsed.", rows.Count);

        return rows;
    }

    private PageAddress GetFirstPage((int ObjectId, int IndexId) id, List<InternalAllocationUnit> allocationUnits)
    {
        var (objectId, indexId) = id;

        var allocationUnitId = IdHelpers.GetAllocationUnitId(objectId, indexId);

        var firstPage = allocationUnits.FirstOrDefault(f => f.AllocationUnitId == allocationUnitId)?.FirstPage?.ToPageAddress();

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