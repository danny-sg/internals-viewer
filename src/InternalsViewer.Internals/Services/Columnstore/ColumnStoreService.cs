using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Columnstore;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Services.Columnstore;

public sealed class ColumnstoreService(IRecordReader recordReader, ILobDataService lobDataService)
{
    private IRecordReader RecordReader { get; } = recordReader;

    private ILobDataService LobDataService { get; } = lobDataService;

    public async Task<ColumnStoreIndex> GetIndex(AllocationUnit allocationUnit,
                                                 DatabaseSource database,
                                                 CancellationToken cancellationToken)
    {
        var rowGroupRecords = await GetRecords("syscsrowgroups", allocationUnit.PartitionId, database, cancellationToken);
        var columnSegmentRecords = await GetRecords("syscscolsegments", allocationUnit.PartitionId, database, cancellationToken);
        var dictionaryRecords = await GetRecords("syscsdictionaries", allocationUnit.PartitionId, database, cancellationToken);

        var structure = TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId);

        var columnMap = structure.Columns.ToDictionary(c => (int)c.ColumnId);

        return ColumnstoreMetadataMapper.Map(allocationUnit, 
                                             rowGroupRecords, 
                                             columnSegmentRecords, 
                                             dictionaryRecords, 
                                             columnMap);
    }

    public async Task<byte[]> GetSegmentData(DatabaseSource database, 
                                             LobPointer lobPointer,
                                             CancellationToken cancellationToken)
    {
        return await LobDataService.GetData(database, new RowIdentifier(lobPointer.PageAddress, (ushort)lobPointer.Slot), cancellationToken);
    }

    private async Task<List<DataRecord>> GetRecords(string name,
                                                    long partitionId,
                                                    DatabaseSource database,
                                                    CancellationToken cancellationToken)
    {
        var allocationUnit = database.AllocationUnits
                                     .Values
                                     .FirstOrDefault(a => a.SchemaName == "sys"
                                                          && a.TableName == name
                                                          && a.AllocationUnitType == AllocationUnitType.InRowData);

        if (allocationUnit is null)
        {
            throw new InvalidOperationException($"sys.{name} allocation unit not found");
        }

        if (allocationUnit.FirstPage == PageAddress.Empty)
        {
            return [];
        }

        var tableStructure = TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId);

        var records = await RecordReader.Read(database,
                                              allocationUnit.FirstPage,
                                              tableStructure,
                                              cancellationToken);

        return [.. records.Where(r => r.GetValue<long>("hobt_id") == partitionId)];
    }
}