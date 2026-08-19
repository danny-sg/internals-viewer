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

    /// <summary>
    /// Reads a segment's prologue alone, which is enough to describe its layout without pulling the whole blob
    /// </summary>
    /// <remarks>
    /// An archive compressed segment has to be expanded before any of it can be read, so it falls back to the full
    /// blob. Everything else costs the root page plus the page holding the first chunk.
    /// </remarks>
    public async Task<SegmentBlobHeader> GetSegmentHeader(DatabaseSource database,
                                                          ColumnSegment segment,
                                                          CancellationToken cancellationToken)
    {
        var pointer = segment.DataPointer;

        var prefix = await LobDataService.GetDataPrefix(database,
                                                        new RowIdentifier(pointer.PageAddress, (ushort)pointer.Slot),
                                                        SegmentBlobHeader.Size + PrologueSlack,
                                                        cancellationToken);

        if (!ArchiveBlobHeader.IsArchive(prefix.Data, prefix.TotalLength))
        {
            return SegmentBlobParser.ParseHeader(prefix.Data);
        }

        var blob = await GetSegmentBlob(database, segment, cancellationToken);

        return blob.Header;
    }

    /// <summary>
    /// Bytes read past the header, covering the two the store by value prologue carries beyond it
    /// </summary>
    private const int PrologueSlack = 16;

    public async Task<SegmentBlob> GetSegmentBlob(DatabaseSource database,
                                                 ColumnSegment segment,
                                                 CancellationToken cancellationToken,
                                                 bool isMarkEnabled = false)
    {
        var data = await GetSegmentData(database, segment.DataPointer, cancellationToken);

        return SegmentBlobParser.Parse(data, isMarkEnabled);
    }

    public async Task<DictionaryBlob> GetDictionaryBlob(DatabaseSource database,
                                                       SegmentDictionary dictionary,
                                                       CancellationToken cancellationToken,
                                                       bool isMarkEnabled = false)
    {
        var data = await GetSegmentData(database, dictionary.DataPointer, cancellationToken);

        return DictionaryBlobParser.Parse(data, (int)dictionary.EntryCount, dictionary.LastId, isMarkEnabled);
    }

    public async Task<SegmentReader> GetSegmentReader(DatabaseSource database,
                                                      ColumnSegment segment,
                                                      CancellationToken cancellationToken)
    {
        var blob = await GetSegmentBlob(database, segment, cancellationToken);

        return new SegmentReader(segment, blob, await GetSegmentDictionary(database, segment, cancellationToken));
    }

    /// <summary>
    /// Resolves a segment's data ids to values, for a caller holding a blob it has already read
    /// </summary>
    public async Task<SegmentValueDecoder> GetSegmentDecoder(DatabaseSource database,
                                                             ColumnSegment segment,
                                                             CancellationToken cancellationToken)
        => new(segment, await GetSegmentDictionary(database, segment, cancellationToken));

    /// <summary>
    /// The dictionary the segment's ids index, a local one taking precedence over the column's global one
    /// </summary>
    private async Task<DictionaryBlob?> GetSegmentDictionary(DatabaseSource database,
                                                             ColumnSegment segment,
                                                             CancellationToken cancellationToken)
    {
        var source = segment.SecondaryDictionaryId >= 0
                     ? segment.LocalDictionary
                     : segment.Column?.GlobalDictionary;

        return source is null ? null : await GetDictionaryBlob(database, source, cancellationToken);
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