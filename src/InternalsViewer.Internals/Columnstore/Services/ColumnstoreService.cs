using System.IO;
using System.Threading;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Columnstore.Parsers;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Columnstore.Services;

public sealed class ColumnstoreService(IRecordReader recordReader, ILobDataService lobDataService)
{
    /// <summary>
    /// Bytes read past the header, covering the two the store by value prologue carries beyond it
    /// </summary>
    private const int PrologueSlack = 16;

    private IRecordReader RecordReader { get; } = recordReader;

    private ILobDataService LobDataService { get; } = lobDataService;

    public async Task<ColumnStoreIndex> GetIndex(AllocationUnit allocationUnit,
                                                 DatabaseSource database,
                                                 CancellationToken cancellationToken)
    {
        var rowGroupRecords = await GetRecords("syscsrowgroups", allocationUnit.PartitionId, database, cancellationToken);
        var columnSegmentRecords = await GetRecords("syscscolsegments", allocationUnit.PartitionId, database, cancellationToken);
        var dictionaryRecords = await GetRecords("syscsdictionaries", allocationUnit.PartitionId, database, cancellationToken);

        // Keyed on the index's own column numbering rather than the table's, the two differing on a nonclustered index
        var structure = IndexStructureProvider.GetIndexStructure(database, allocationUnit.AllocationUnitId);

        var columnMap = structure.Columns
                                 .Where(c => c.IndexColumnId > 0)
                                 .GroupBy(c => c.IndexColumnId)
                                 .ToDictionary(g => g.Key, ColumnStructure (g) => g.First());

        var related = database.AllocationUnits
                              .Values
                              .Where(a => a.ObjectId == allocationUnit.ObjectId
                                          && a.IndexId == allocationUnit.IndexId
                                          && a.PartitionNumber == allocationUnit.PartitionNumber)
                              .ToList();

        return ColumnstoreMetadataMapper.Map(allocationUnit,
                                             rowGroupRecords,
                                             columnSegmentRecords,
                                             dictionaryRecords,
                                             columnMap,
                                             related,
                                             GetLocatorNames(database, allocationUnit, structure));
    }

    public async Task<byte[]> GetData(DatabaseSource database,
                                      LobPointer lobPointer,
                                      CancellationToken cancellationToken)
    {
        return await LobDataService.GetData(database,
                                            new RowIdentifier(lobPointer.PageAddress, (ushort)lobPointer.Slot),
                                            cancellationToken);
    }

    /// <summary>
    /// Reads a segment prologue alone, which is enough to describe its layout without pulling the whole blob
    /// </summary>
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

    public async Task<SegmentBlob> GetSegmentBlob(DatabaseSource database,
                                                 ColumnSegment segment,
                                                 CancellationToken cancellationToken,
                                                 bool isMarkEnabled = false)
    {
        var data = await GetData(database, segment.DataPointer, cancellationToken);

        return SegmentBlobParser.Parse(data, segment, isMarkEnabled);
    }

    /// <summary>
    /// How a string dictionary's pages are coded, read without pulling the whole dictionary in
    /// </summary>
    /// <remarks>
    /// Two reads rather than one, the first being what says how far in the pages start. A numeric dictionary has no
    /// pages and answers null, as does an archive compressed one whose bytes cannot be read a prefix at a time.
    /// </remarks>
    public async Task<DictionaryHeaderInfo> GetDictionaryCoding(DatabaseSource database,
                                                                SegmentDictionary dictionary,
                                                                CancellationToken cancellationToken)
    {
        var pointer = dictionary.DataPointer;

        var identifier = new RowIdentifier(pointer.PageAddress, (ushort)pointer.Slot);

        var header = await LobDataService.GetDataPrefix(database,
                                                        identifier,
                                                        StringDictionary.HandleArrayOffset,
                                                        cancellationToken);

        if (ArchiveBlobHeader.IsArchive(header.Data, header.TotalLength))
        {
            return new DictionaryHeaderInfo(null, 0);
        }

        var pageCount = DictionaryBlobParser.GetPageCount(header.Data);

        if (DictionaryBlobParser.GetFirstPageOffset(header.Data) is not { } offset)
        {
            return new DictionaryHeaderInfo(null, pageCount);
        }

        var pages = await LobDataService.GetDataPrefix(database, identifier, offset + 4, cancellationToken);

        return new DictionaryHeaderInfo(DictionaryBlobParser.ParsePageCoding(pages.Data, offset), pageCount);
    }

    public async Task<DictionaryBlob> GetDictionaryBlob(DatabaseSource database,
                                                        SegmentDictionary dictionary,
                                                        CancellationToken cancellationToken,
                                                        bool isMarkEnabled = false)
    {
        var data = await GetData(database, dictionary.DataPointer, cancellationToken);

        var blob = DictionaryBlobParser.Parse(data, (int)dictionary.EntryCount, dictionary.LastId, isMarkEnabled);

        if (blob is StringDictionary strings)
        {
            await ReadLobValues(database, strings, cancellationToken);
        }

        return blob;
    }

    public async Task<SegmentReader> GetSegmentReader(DatabaseSource database,
                                                      ColumnSegment segment,
                                                      CancellationToken cancellationToken)
    {
        var blob = await GetSegmentBlob(database, segment, cancellationToken);

        return new SegmentReader(segment, blob, await GetSegmentDictionary(database, segment, cancellationToken));
    }

    /// <summary>
    /// Resolves a segment data ids to values
    /// </summary>
    public async Task<SegmentValueDecoder> GetSegmentDecoder(DatabaseSource database,
                                                             ColumnSegment segment,
                                                             CancellationToken cancellationToken)
        => new(segment, await GetSegmentDictionary(database, segment, cancellationToken));

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

    public async Task ResolveDeltaStoreRowGroups(DatabaseSource database, CancellationToken cancellationToken)
    {
        var deltaStores = database.AllocationUnits
                                  .Values
                                  .Where(a => a.OwnerType == (byte)ColumnstoreRowsetType.DeltaStore)
                                  .ToList();

        if (deltaStores.Count == 0)
        {
            return;
        }

        var records = await GetRecords("syscsrowgroups", database, cancellationToken);

        var rowGroupByDeltaStore = new Dictionary<long, int>();

        foreach (var record in records)
        {
            rowGroupByDeltaStore.TryAdd(record.GetValue<long>("ds_hobtid"), record.GetValue<int>("segment_id"));
        }

        foreach (var deltaStore in deltaStores)
        {
            if (rowGroupByDeltaStore.TryGetValue(deltaStore.PartitionId, out var rowGroupId))
            {
                deltaStore.DeltaStoreRowGroupId = rowGroupId;
            }
        }
    }

    /// <summary>
    /// Follows the pointers a string dictionary holds in place of values too big for its string store
    /// </summary>
    /// <remarks>
    /// The parser cannot do this, a pointer needing a read of its own, so it happens once the blob is loaded. A
    /// LOB payload carries the same Xpress Huffman envelope as archive compression even on a plain index.
    /// </remarks>
    private async Task ReadLobValues(DatabaseSource database,
        StringDictionary dictionary,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < dictionary.Handles.Length; i++)
        {
            if (!dictionary.TryGetLobPointer(i, out var pointer))
            {
                continue;
            }

            var data = await GetData(database,
                new LobPointer(pointer.BlobId, pointer.PageAddress, pointer.Slot),
                cancellationToken);

            ReadOnlyMemory<byte> payload = data;

            dictionary.LobValues[i] = ArchiveBlobHeader.IsArchive(payload.Span)
                ? ArchiveBlobExpander.Expand(payload).ToArray()
                : data;
        }
    }

    /// <summary>
    /// The clustered key columns a nonclustered index has to keep, being the ones it does not already hold
    /// </summary>
    private static List<string> GetLocatorNames(DatabaseSource database,
                                                AllocationUnit allocationUnit,
                                                IndexStructure indexStructure)
    {
        if (allocationUnit.IndexType != IndexType.NonClusteredColumnStore)
        {
            return [];
        }

        if (allocationUnit.ParentIndexType != IndexType.Clustered)
        {
            return ["RID"];
        }

        var clustered = database.AllocationUnits
                                .Values
                                .FirstOrDefault(a => a.ObjectId == allocationUnit.ObjectId && a.IndexId == 1);

        if (clustered is null)
        {
            return [];
        }

        // Matched on name, the column ids of one index structure not meaning the same thing in another
        var held = indexStructure.Columns
                                 .Where(c => c.IsIncludeColumn)
                                 .Select(c => c.ColumnName)
                                 .ToHashSet();

        var key = IndexStructureProvider.GetIndexStructure(database, clustered.AllocationUnitId).Columns;

        var names = key.Where(c => c.IsIndexKey && !held.Contains(c.ColumnName))
                       .Select(c => c.ColumnName)
                       .ToList();

        // The uniqueifier a non unique clustered index adds comes after its key, whatever order it is listed in
        names.AddRange(key.Where(c => c.IsUniqueifier).Select(c => c.ColumnName));

        return names;
    }

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

    private async Task<List<Record>> GetRecords(string name,
                                                long partitionId,
                                                DatabaseSource database,
                                                CancellationToken cancellationToken)
    {
        var records = await GetRecords(name, database, cancellationToken);

        return [.. records.Where(r => r.GetValue<long>("hobt_id") == partitionId)];
    }

    private async Task<List<Record>> GetRecords(string name,
                                                DatabaseSource database,
                                                CancellationToken cancellationToken)
    {
        var allocationUnit = database.AllocationUnits
                                     .Values
                                     .FirstOrDefault(a => a.SchemaName == "sys"
                                                          && a.TableName == name
                                                          && a.AllocationUnitType == AllocationUnitType.InRowData)
                             ?? throw new InvalidOperationException($"sys.{name} allocation unit not found");

        if (allocationUnit.FirstPage == PageAddress.Empty)
        {
            return [];
        }

        var tableStructure = TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId);

        return await RecordReader.Read(database,
                                       allocationUnit.FirstPage,
                                       tableStructure,
                                       cancellationToken);
    }
}