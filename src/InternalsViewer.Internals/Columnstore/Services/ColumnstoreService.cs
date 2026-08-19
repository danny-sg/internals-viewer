using System.IO;
using System.Threading;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Parsers;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Columnstore.Services;

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

        var related = database.AllocationUnits
                              .Values
                              .Where(a => a.ObjectId == allocationUnit.ObjectId && a.IndexId == allocationUnit.IndexId)
                              .ToList();

        return ColumnstoreMetadataMapper.Map(allocationUnit,
                                             rowGroupRecords,
                                             columnSegmentRecords,
                                             dictionaryRecords,
                                             columnMap,
                                             related);
    }

    public async Task<byte[]> GetSegmentData(DatabaseSource database,
                                             LobPointer lobPointer,
                                             CancellationToken cancellationToken)
    {
        return await LobDataService.GetData(database,
                                            new RowIdentifier(lobPointer.PageAddress, (ushort)lobPointer.Slot),
                                            cancellationToken);
    }

    public async Task<SegmentBlob> GetSegmentBlob(DatabaseSource database,
                                                 ColumnSegment segment,
                                                 CancellationToken cancellationToken)
    {
        var data = await GetSegmentData(database, segment.DataPointer, cancellationToken);

        return SegmentBlobParser.Parse(data);
    }

    public async Task<DictionaryBlob> GetDictionaryBlob(DatabaseSource database,
                                                       SegmentDictionary dictionary,
                                                       CancellationToken cancellationToken)
    {
        var data = await GetSegmentData(database, dictionary.DataPointer, cancellationToken);

        return DictionaryBlobParser.Parse(data, (int)dictionary.EntryCount, dictionary.LastId);
    }

    public async Task<SegmentReader> GetSegmentReader(DatabaseSource database,
                                                      ColumnSegment segment,
                                                      CancellationToken cancellationToken)
    {
        var blob = await GetSegmentBlob(database, segment, cancellationToken);

        var source = segment.SecondaryDictionaryId >= 0
                     ? segment.LocalDictionary
                     : segment.Column?.GlobalDictionary;

        var dictionary = source is null ? null : await GetDictionaryBlob(database, source, cancellationToken);

        return new SegmentReader(segment, blob, dictionary);
    }

    public async Task<RowGroupReader> GetRowGroupReader(DatabaseSource database,
                                                       RowGroup rowGroup,
                                                       CancellationToken cancellationToken)
    {
        var readers = new List<SegmentReader>();

        var skipped = new List<ColumnSegment>();

        foreach (var segment in rowGroup.Segments)
        {
            try
            {
                readers.Add(await GetSegmentReader(database, segment, cancellationToken));
            }
            catch (InvalidDataException)
            {
                skipped.Add(segment);
            }
        }

        return new RowGroupReader(rowGroup, readers, skipped);
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