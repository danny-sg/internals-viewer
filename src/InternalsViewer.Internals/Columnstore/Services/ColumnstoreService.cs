using System.Diagnostics;
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

public sealed class ColumnstoreService(IRecordReader recordReader,
                                       ILobDataService lobDataService,
                                       ColumnstoreCache? cache = null,
                                       ILogger<ColumnstoreService>? logger = null)
{
    /// <summary>
    /// Bytes read past the header, covering the two the store by value prologue carries beyond it
    /// </summary>
    private const int PrologueSlack = 16;

    private const int PrefixProbeSize = 8192;

    private IRecordReader RecordReader { get; } = recordReader;

    private ILobDataService LobDataService { get; } = lobDataService;

    private ILogger<ColumnstoreService>? Logger { get; } = logger;

    /// <summary>
    /// Raised for each page a columnstore read touches, with the structure it was read for
    /// </summary>
    public Action<ColumnstorePageRead>? PageRead { get; set; }

    private Action<PageAddress> SegmentSink(DatabaseSource database, ColumnSegment segment)
        => Sink(database,
                new ColumnstorePageRead(PageAddress.Empty,
                                        segment.Key.RowGroupId,
                                        segment.Key.ColumnId,
                                        segment.Column?.Name ?? string.Empty,
                                        segment.Key.RowGroupId,
                                        -1,
                                        ColumnstoreReadType.Segment));

    private Action<PageAddress> DictionarySink(DatabaseSource database, SegmentDictionary dictionary)
        => Sink(database,
                new ColumnstorePageRead(PageAddress.Empty,
                                        -1,
                                        dictionary.ColumnId,
                                        string.Empty,
                                        -1,
                                        dictionary.DictionaryId,
                                        ColumnstoreReadType.Dictionary));

    private Action<PageAddress> Sink(DatabaseSource database, ColumnstorePageRead template)
        => address =>
        {
            var read = template with { PageAddress = address };

            cache?.SetPageRead(database, read);

            PageRead?.Invoke(read);
        };

    public async Task<ColumnStoreIndex> GetIndex(AllocationUnit allocationUnit,
                                                 DatabaseSource database,
                                                 CancellationToken cancellationToken)
    {
        if (cache?.GetIndex(database, allocationUnit.AllocationUnitId) is { } cached)
        {
            return cached;
        }

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

        var index = ColumnstoreMetadataMapper.Map(allocationUnit,
                                                  rowGroupRecords,
                                                  columnSegmentRecords,
                                                  dictionaryRecords,
                                                  columnMap,
                                                  related,
                                                  GetLocatorNames(database, allocationUnit, structure));

        cache?.SetIndex(database, allocationUnit.AllocationUnitId, index);

        return index;
    }

    public async Task<byte[]> GetData(DatabaseSource database,
                                      LobPointer lobPointer,
                                      CancellationToken cancellationToken,
                                      Action<PageAddress>? onPageRead = null)
    {
        var identifier = new RowIdentifier(lobPointer.PageAddress, (ushort)lobPointer.Slot);

        if (cache?.GetData(database, identifier) is { } cached)
        {
            return cached;
        }

        var start = Stopwatch.GetTimestamp();

        var data = await LobDataService.GetData(database, identifier, cancellationToken, onPageRead);

        Logger?.LogDebug("Read {Bytes} bytes of lob data from {Page} in {Duration}",
                         data.Length,
                         lobPointer.PageAddress,
                         Stopwatch.GetElapsedTime(start));

        cache?.SetData(database, identifier, data);

        return data;
    }

    public async Task<SegmentBlobHeader> GetSegmentHeader(DatabaseSource database,
                                                          ColumnSegment segment,
                                                          CancellationToken cancellationToken)
    {
        var pointer = segment.DataPointer;

        var start = Stopwatch.GetTimestamp();

        var prefix = await LobDataService.GetDataPrefix(database,
                                                        new RowIdentifier(pointer.PageAddress, (ushort)pointer.Slot),
                                                        SegmentBlobHeader.Size + PrologueSlack,
                                                        cancellationToken,
                                                        SegmentSink(database, segment));

        if (!ArchiveBlobHeader.IsArchive(prefix.Data, prefix.TotalLength))
        {
            var header = SegmentBlobParser.ParseHeader(prefix.Data);

            Logger?.LogDebug("Read header for row group {RowGroup} column {Column} from a {Bytes} byte prefix in {Duration}",
                             segment.Key.RowGroupId,
                             segment.Key.ColumnId,
                             prefix.Data.Length,
                             Stopwatch.GetElapsedTime(start));

            return header;
        }

        var blob = await GetSegmentBlob(database, segment, cancellationToken, depth: SegmentLoadDepth.Header);

        Logger?.LogDebug("Read header for row group {RowGroup} column {Column} through the whole archive blob in {Duration}",
                         segment.Key.RowGroupId,
                         segment.Key.ColumnId,
                         Stopwatch.GetElapsedTime(start));

        return blob.Header;
    }

    public async Task<SegmentBlob> GetSegmentBlob(DatabaseSource database,
                                                 ColumnSegment segment,
                                                 CancellationToken cancellationToken,
                                                 bool isMarkEnabled = false,
                                                 SegmentLoadDepth depth = SegmentLoadDepth.Full)
    {
        var sink = SegmentSink(database, segment);

        var data = depth == SegmentLoadDepth.Full
                   ? await GetData(database, segment.DataPointer, cancellationToken, sink)
                   : await GetPartialData(database, segment, depth, cancellationToken);

        var start = Stopwatch.GetTimestamp();

        var blob = SegmentBlobParser.Parse(data, segment, isMarkEnabled, depth);

        Logger?.LogDebug("Parsed {Bytes} byte segment for row group {RowGroup} column {Column} to {Depth} in {Duration}",
                         data.Length,
                         segment.Key.RowGroupId,
                         segment.Key.ColumnId,
                         depth,
                         Stopwatch.GetElapsedTime(start));

        return blob;
    }

    private async Task<byte[]> GetPartialData(DatabaseSource database,
                                              ColumnSegment segment,
                                              SegmentLoadDepth depth,
                                              CancellationToken cancellationToken)
    {
        var pointer = segment.DataPointer;

        var identifier = new RowIdentifier(pointer.PageAddress, (ushort)pointer.Slot);

        if (cache?.GetData(database, identifier) is { } cached)
        {
            return cached;
        }

        var start = Stopwatch.GetTimestamp();

        var sink = SegmentSink(database, segment);

        var probe = await LobDataService.GetDataPrefix(database, identifier, PrefixProbeSize, cancellationToken, sink);

        if (ArchiveBlobHeader.IsArchive(probe.Data, probe.TotalLength))
        {
            return await GetData(database, pointer, cancellationToken, sink);
        }

        var required = SegmentBlobParser.GetRequiredLength(probe.Data, segment, depth);

        if (required <= 0 || required > probe.TotalLength)
        {
            return await GetData(database, pointer, cancellationToken, sink);
        }

        var prefix = required <= probe.Data.Length
                     ? probe
                     : await LobDataService.GetDataPrefix(database, identifier, required, cancellationToken, sink);

        Logger?.LogDebug("Read {Bytes} of {Total} bytes for row group {RowGroup} column {Column} to {Depth} in {Duration}",
                         required,
                         prefix.TotalLength,
                         segment.Key.RowGroupId,
                         segment.Key.ColumnId,
                         depth,
                         Stopwatch.GetElapsedTime(start));

        return prefix.Data;
    }

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
        var data = await GetData(database,
                                 dictionary.DataPointer,
                                 cancellationToken,
                                 DictionarySink(database, dictionary));

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

        var (dictionary, overflow) = await GetSegmentDictionaries(database, segment, cancellationToken);

        return new SegmentReader(segment, blob, dictionary, overflow);
    }

    public async Task<SegmentValueDecoder> GetSegmentDecoder(DatabaseSource database,
                                                             ColumnSegment segment,
                                                             CancellationToken cancellationToken)
    {
        var (dictionary, overflow) = await GetSegmentDictionaries(database, segment, cancellationToken);

        return new SegmentValueDecoder(segment, dictionary, overflow);
    }

    public Task<RowGroupReader> GetRowGroupReader(DatabaseSource database,
                                                 RowGroup rowGroup,
                                                 CancellationToken cancellationToken)
        => GetRowGroupReader(database, rowGroup, null, cancellationToken);

    public async Task<RowGroupReader> GetRowGroupReader(DatabaseSource database,
                                                       RowGroup rowGroup,
                                                       IReadOnlyCollection<int>? columnIds,
                                                       CancellationToken cancellationToken)
    {
        var readers = new List<SegmentReader>();

        var skipped = new List<ColumnSegment>();

        var wanted = rowGroup.Segments.Where(s => columnIds is null
                                                  || s.Column is null
                                                  || columnIds.Contains(s.Column.ColumnStoreColumnId));

        foreach (var segment in wanted)
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

    public async Task<DeletedRows> GetDeletedRows(DatabaseSource database,
                                                 ColumnStoreIndex index,
                                                 CancellationToken cancellationToken)
    {
        if (index.DeleteBitmapAllocationUnit is not { } allocationUnit || allocationUnit.FirstPage == PageAddress.Empty)
        {
            return DeletedRows.None;
        }

        var structure = TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId);

        var records = await RecordReader.Read(database, allocationUnit.FirstPage, structure, cancellationToken);

        var byRowGroup = new Dictionary<int, List<int>>();

        foreach (var record in records)
        {
            if (record.IsGhost || record.Fields.Count < 2)
            {
                continue;
            }

            var rowGroupId = record.Fields[0].GetValue<int>();

            var rowOrdinal = record.Fields[1].GetValue<int>();

            if (!byRowGroup.TryGetValue(rowGroupId, out var rows))
            {
                rows = [];

                byRowGroup[rowGroupId] = rows;
            }

            rows.Add(rowOrdinal);
        }

        return new DeletedRows(byRowGroup.ToDictionary(g => g.Key, g =>
        {
            var rows = g.Value.ToArray();

            Array.Sort(rows);

            return rows;
        }));
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
    /// Get Dictionaries linked to the Segment
    /// </summary>
    private async Task<(DictionaryBlob? Dictionary, DictionaryBlob? Overflow)> 
        GetSegmentDictionaries(DatabaseSource database,
                               ColumnSegment segment,
                               CancellationToken cancellationToken)
    {
        var primary = segment.PrimaryDictionaryId >= 0 ? segment.Column?.GlobalDictionary : null;

        var secondary = segment.SecondaryDictionaryId >= 0 ? segment.LocalDictionary : null;

        if (primary is null)
        {
            return (secondary is null ? null : await GetDictionaryBlob(database, secondary, cancellationToken), null);
        }

        return (await GetDictionaryBlob(database, primary, cancellationToken),
                secondary is null ? null : await GetDictionaryBlob(database, secondary, cancellationToken));
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