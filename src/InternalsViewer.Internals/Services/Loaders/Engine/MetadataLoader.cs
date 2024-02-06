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
public class MetadataLoader(ILogger<MetadataLoader> logger, IRecordReader recordReader)
    : IMetadataLoader
{
    private ILogger<MetadataLoader> Logger { get; } = logger;

    private IRecordReader RecordReader { get; } = recordReader;

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
    /// sys.sysallocunits  - <see cref="InternalAllocationUnit"/>
    ///
    ///     --> sys.sysrowsets  - <see cref="InternalRowSet"/>
    ///                         
    ///     --> sys.sysschobjs  - <see cref="InternalObject"/>
    ///                         
    ///     --> sys.sysrscols   - <see cref="InternalColumnLayout"/>
    ///                         
    ///     --> sys.syscolpars  - <see cref="InternalColumn"/>
    ///     
    ///     --> sys.sysclsobjs  - <see cref="InternalEntityObject"/>
    ///                         
    ///     --> sys.sysidxstats - <see cref="InternalIndex"/>
    ///                         
    ///     --> sys.sysiscols   - <see cref="InternalIndexColumn"/>
    ///                         
    ///     --> sys.sysprufiles - <see cref="InternalFile"/>
    /// </remarks>              
    public async Task<InternalMetadata> Load(DatabaseSource database)
    {
        var result = new InternalMetadata();

        Logger.LogDebug("Database: {DatabaseName} - Building metadata", database.Name);

        result.AllocationUnits = await GetAllocationUnits(database);

        var rowSetsFirstPage = GetFirstPage(InternalTableConstants.RowSetId, result.AllocationUnits);

        result.RowSets = await GetRowSets(rowSetsFirstPage, database);

        var objectsFirstPage = GetFirstPage(InternalTableConstants.ObjectsId, result.AllocationUnits);

        result.Objects = await GetObjects(objectsFirstPage, database);

        var columnLayouts = GetFirstPage(InternalTableConstants.ColumnLayoutsId, result.AllocationUnits);

        result.ColumnLayouts = await GetColumnLayouts(columnLayouts, database);

        var columns = GetFirstPage(InternalTableConstants.ColumnsId, result.AllocationUnits);

        result.Columns = await GetColumns(columns, database);

        var entitiesFirstPage = GetFirstPage(InternalTableConstants.EntitiesId, result.AllocationUnits);

        result.Entities = await GetEntities(entitiesFirstPage, database);

        var indexesFirstPage = GetFirstPage(InternalTableConstants.IndexesId, result.AllocationUnits);

        result.Indexes = await GetIndexes(indexesFirstPage, database);

        var indexColumnsFirstPage = GetFirstPage(InternalTableConstants.IndexColumnsId, result.AllocationUnits);

        result.IndexColumns = await GetIndexColumns(indexColumnsFirstPage, database);

        var filesFirstPage = GetFirstPage(InternalTableConstants.FilesId, result.AllocationUnits);

        result.Files = await GetFiles(filesFirstPage, database);

        return result;
    }

    private async Task<List<InternalFile>> GetFiles(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Files (sys.sysprufiles) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalFileStructure.GetStructure(-1));

        Logger.LogTrace("Files (sys.sysprufiles): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalFileLoader.Load).ToList();

        Logger.LogDebug("Files (sys.sysprufiles): {Count} records parsed.", rows.Count);

        return rows;

    }

    private async Task<List<InternalEntityObject>> GetEntities(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Entities (sys.sysclsobjs) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalEntityObjectStructure.GetStructure(-1));

        Logger.LogTrace("Entities (sys.sysclsobjs): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalEntityObjectLoader.Load).ToList();

        Logger.LogDebug("Entities (sys.sysclsobjs): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalObject>> GetObjects(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Objects (sys.sysschobjs) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalObjectStructure.GetStructure(-1));

        Logger.LogTrace("Objects (sys.sysschobjs): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalObjectLoader.Load).ToList();

        Logger.LogDebug("Objects (sys.sysschobjs): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalIndex>> GetIndexes(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Indexes (sys.sysidxstats) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalIndexStructure.GetStructure(-1));

        Logger.LogTrace("Indexes (sys.sysidxstats): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalIndexLoader.Load).ToList();

        Logger.LogDebug("Indexes (sys.sysidxstats): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalIndexColumn>> GetIndexColumns(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Index Columns (sys.sysiscols) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                              pageAddress,
                                              InternalIndexColumnStructure.GetStructure(-1));

        Logger.LogTrace("Index Columns (sys.sysiscols): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalIndexColumnLoader.Load).ToList();

        Logger.LogDebug("Index Columns (sys.sysiscols): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalRowSet>> GetRowSets(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Row Sets (sys.sysrowsets) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalRowSetStructure.GetStructure(-1));

        Logger.LogTrace("Row Sets (sys.sysrowsets): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalRowSetLoader.Load).ToList();

        Logger.LogDebug("Row Sets (sys.sysrowsets): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalColumnLayout>> GetColumnLayouts(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Column Layouts (sys.sysrscols) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalColumnLayoutStructure.GetStructure(-1));

        Logger.LogTrace("Column Layouts (sys.sysrscols): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalColumnLayoutLoader.Load).ToList();

        Logger.LogDebug("Column Layouts (sys.sysrscols): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalColumn>> GetColumns(PageAddress pageAddress, DatabaseSource database)
    {
        Logger.LogTrace("Getting Columns (sys.syscolpars) using fixed Object Id/Index Id");

        var records = await RecordReader.Read(database,
                                             pageAddress,
                                             InternalColumnStructure.GetStructure(-1));

        Logger.LogTrace("Columns (sys.syscolpars): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalColumnLoader.Load).ToList();

        Logger.LogDebug("Columns (sys.syscolpars): {Count} records parsed.", rows.Count);

        return rows;
    }

    private async Task<List<InternalAllocationUnit>> GetAllocationUnits(DatabaseSource databaseDetail)
    {
        var (objectId, indexId) = InternalTableConstants.ObjectsId;

        var pageAddress = databaseDetail.BootPage.FirstAllocationUnitsPage;

        Logger.LogTrace("Getting Allocation Units (sys.sysallocunits) using first page specified in Boot Page (1:9): {PageAddress}",
                        pageAddress);

        var id = IdHelpers.GetAllocationUnitId(objectId, indexId);

        var records = await RecordReader.Read(databaseDetail,
                                             databaseDetail.BootPage.FirstAllocationUnitsPage,
                                             InternalAllocationUnitStructure.GetStructure(id));

        Logger.LogTrace("Allocation Units (sys.sysallocunits): {Count} records found. Parsing records...", records.Count);

        var rows = records.Select(InternalAllocationUnitLoader.Load).ToList();

        Logger.LogDebug("Allocation Units (sys.sysallocunits): {Count} records parsed.", rows.Count);

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