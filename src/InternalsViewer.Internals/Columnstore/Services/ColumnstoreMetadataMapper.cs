using System.Buffers.Binary;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Columnstore.Services;

/// <summary>
/// Maps raw system table records to the columnstore object model
/// </summary>
public static class ColumnstoreMetadataMapper
{
    /// <summary>
    /// has_nulls is derived from bit 0 of the segment status field.
    /// </summary>
    private const int StatusHasNullsFlag = 1;

    private const int RowGroupStateMask = 0x7;

    /// <summary>
    /// How far the columnstore's own column numbering runs ahead of the index column it stands for
    /// </summary>
    /// <remarks>
    /// A clustered columnstore numbers from two, leaving its first slot unused. A nonclustered one numbers from one
    /// and puts its locator one past its last index column, which is the column that resolves to no structure.
    /// </remarks>
    public static int GetColumnIdOffset(IndexType indexType)
        => indexType == IndexType.NonClusteredColumnStore ? 0 : 1;

    public static ColumnStoreIndex Map(AllocationUnit allocationUnit,
                                       IReadOnlyList<Record> rowGroupRecords,
                                       IReadOnlyList<Record> segmentRecords,
                                       IReadOnlyList<Record> dictionaryRecords,
                                       IReadOnlyDictionary<int, ColumnStructure>? columnMap = null,
                                       IEnumerable<AllocationUnit>? relatedAllocationUnits = null,
                                       IReadOnlyList<string>? locatorNames = null)
    {
        var index = new ColumnStoreIndex
        {
            HobtId = allocationUnit.PartitionId,
            ObjectId = allocationUnit.ObjectId,
            IndexId = allocationUnit.IndexId,
            IndexName = allocationUnit.IndexName,
            SchemaName = allocationUnit.SchemaName,
            TableName = allocationUnit.TableName,
            IsClustered = allocationUnit.IndexType == IndexType.ClusteredColumnStore
        };

        index.Rowsets.AddRange(BuildRowsets(relatedAllocationUnits ?? [allocationUnit]));

        var dictionaries = dictionaryRecords.Select(MapDictionary).ToList();

        var globalDictionaries = dictionaries.Where(d => d.IsGlobal)
                                             .ToDictionary(d => d.ColumnId);

        var localDictionaries = dictionaries
            .Where(d => !d.IsGlobal)
            .ToDictionary(d => (d.ColumnId, d.DictionaryId));

        var segments = segmentRecords.Select(MapSegment).ToList();

        index.Columns.AddRange(BuildColumns(segments,
                                            globalDictionaries,
                                            columnMap,
                                            allocationUnit.IndexType,
                                            allocationUnit.ParentIndexType,
                                            locatorNames ?? []));

        var columnsById = index.Columns.ToDictionary(c => c.ColumnStoreColumnId);

        foreach (var segment in segments)
        {
            if (columnsById.TryGetValue(segment.Key.ColumnId, out var column))
            {
                segment.Column = column;
            }

            if (segment.SecondaryDictionaryId >= 0
                && localDictionaries.TryGetValue(
                    (segment.Key.ColumnId, segment.SecondaryDictionaryId), out var local))
            {
                segment.LocalDictionary = local;
            }
        }

        var segmentsByRowGroup = segments.GroupBy(s => s.Key.RowGroupId)
                                         .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Key.ColumnId).ToList());

        foreach (var record in rowGroupRecords)
        {
            var rowGroup = MapRowGroup(record, allocationUnit.PartitionId);

            if (segmentsByRowGroup.TryGetValue(rowGroup.RowGroupId, out var rowGroupSegments))
            {
                rowGroup.Segments.AddRange(rowGroupSegments);
            }

            index.RowGroups.Add(rowGroup);
        }

        var orphaned = segmentsByRowGroup.Keys
                                         .Except(index.RowGroups.Select(r => r.RowGroupId))
                                         .ToList();

        if (orphaned.Count > 0)
        {
            throw new InvalidOperationException(
                $"Segments reference row groups {string.Join(", ", orphaned)} "
                + $"that are absent from syscsrowgroups for hobt {allocationUnit.PartitionId}.");
        }

        index.RowGroups.Sort((a, b) => a.RowGroupId.CompareTo(b.RowGroupId));

        return index;
    }

    /// <summary>
    /// Decodes a 16-byte LOB locator: 8 byte blob id, 4 byte page, 2 byte file, 2 byte slot.
    /// </summary>
    public static LobPointer DecodeLobPointer(byte[]? value)
    {
        if (value is null || value.Length < 16)
        {
            return default;
        }

        var span = value.AsSpan();

        var blobId = BinaryPrimitives.ReadInt64LittleEndian(span[..8]);
        var pageId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
        var fileId = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(12, 2));
        var slot = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(14, 2));

        return new LobPointer(blobId, new PageAddress(fileId, pageId), slot);
    }

    /// <summary>
    /// What a nonclustered index has to keep to find its way back, which is the RID over a heap and the key otherwise
    /// </summary>
    private static string DescribeLocator(IndexType? parentIndexType) => parentIndexType switch
    {
        IndexType.Heap => "RID",
        IndexType.Clustered => "Clustered Key",
        _ => string.Empty
    };
    
    /// <summary>
    /// Columnstore column ids are offset from the table column ids they map to
    /// </summary>
    /// <summary>
    /// A locator named after the key column it holds, falling back to a number when the key cannot be read
    /// </summary>
    private static string NameLocator(bool isLocator, int ordinal, int count, IReadOnlyList<string> names)
    {
        if (!isLocator)
        {
            return string.Empty;
        }

        if (ordinal <= names.Count)
        {
            return names[ordinal - 1];
        }

        return count > 1 ? $"Row Locator {ordinal}" : "Row Locator";
    }

    /// <summary>
    /// Groups the allocation units of an index by the row set they belong to
    /// </summary>
    private static IEnumerable<ColumnstoreRowset> BuildRowsets(IEnumerable<AllocationUnit> allocationUnits)
    {
        var grouped = allocationUnits.GroupBy(a => a.PartitionId)
                                     .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var columnstoreRowset = new ColumnstoreRowset
            {
                HobtId = group.Key,
                RowsetType = (ColumnstoreRowsetType)group.First().OwnerType
            };

            columnstoreRowset.AllocationUnits.AddRange(group.OrderBy(a => a.AllocationUnitType));

            yield return columnstoreRowset;
        }
    }

    private static IEnumerable<ColumnStoreColumn> BuildColumns(IEnumerable<ColumnSegment> segments,
                                                               IReadOnlyDictionary<int, SegmentDictionary> globalDictionaries,
                                                               IReadOnlyDictionary<int, ColumnStructure>? columnStructures,
                                                               IndexType indexType,
                                                               IndexType? parentIndexType,
                                                               IReadOnlyList<string> locatorNames)
    {
        var offset = GetColumnIdOffset(indexType);

        var columnIds = segments.Select(s => s.Key.ColumnId).Distinct().OrderBy(id => id).ToList();

        bool IsLocator(int columnId)
            => indexType == IndexType.NonClusteredColumnStore
               && !(columnStructures?.ContainsKey(columnId - offset) ?? false);

        // A composite clustered key is kept a column at a time, so there is one locator per key column
        var locatorCount = columnIds.Count(IsLocator);

        var locatorOrdinal = 0;

        foreach (var columnId in columnIds)
        {
            ColumnStructure? structure = null;

            columnStructures?.TryGetValue(columnId - offset, out structure);

            var isLocator = IsLocator(columnId);

            if (isLocator)
            {
                locatorOrdinal++;
            }

            var column = new ColumnStoreColumn
            {
                ColumnStoreColumnId = columnId,
                Structure = structure,
                IsLocator = isLocator,
                LocatorName = NameLocator(isLocator, locatorOrdinal, locatorCount, locatorNames),
                LocatorDescription = DescribeLocator(parentIndexType),
                GlobalDictionary = globalDictionaries.GetValueOrDefault(columnId)
            };

            yield return column;
        }
    }

    private static RowGroup MapRowGroup(Record record, long hobtId)
    {
        var rawStatus = record.GetValue<int>(RowGroupColumns.Status);

        var state = (RowGroupState)(rawStatus & RowGroupStateMask);

        return new RowGroup
        {
            HobtId = hobtId,
            PartitionId = hobtId,
            RowGroupId = record.GetValue<int>(RowGroupColumns.SegmentId),
            State = state,
            RawStatus = rawStatus,
            TotalRows = record.GetValue<int>(RowGroupColumns.RowCount),
            Version = record.GetValue<int>(RowGroupColumns.Version),
            Flags = record.GetValue<int>(RowGroupColumns.Flags),
            CompressedReason = record.GetValue<int>(RowGroupColumns.CompressedReason),
            Generation = record.GetValue<long>(RowGroupColumns.Generation),
            DeltaStoreHobtId = record.GetValue<long>(RowGroupColumns.DeltaStoreHobtId),
            CreatedTime = record.GetValue<DateTime?>(RowGroupColumns.CreatedTime),
            ClosedTime = record.GetValue<DateTime?>(RowGroupColumns.ClosedTime),
            MetadataBlob = new RowGroupMetadataPointer(
                record.GetValue<short>(RowGroupColumns.ContainerId),
                DecodeLobPointer(record.GetValue<byte[]?>(RowGroupColumns.BlobId)),
                record.GetValue<int>(RowGroupColumns.MetadataOffset),
                record.GetValue<int>(RowGroupColumns.MetadataSize))
        };
    }

    private static ColumnSegment MapSegment(Record record)
    {
        var hobtId = record.GetValue<long>(SegmentColumns.HobtId);
        var status = record.GetValue<int>(SegmentColumns.Status);

        var segment = new ColumnSegment
        {
            Key = new SegmentKey(hobtId,
                                 hobtId,
                                 record.GetValue<int>(SegmentColumns.SegmentId),
                                 record.GetValue<int>(SegmentColumns.ColumnId)),

            Version = record.GetValue<int>(SegmentColumns.Version),
            Encoding = (SegmentEncoding)record.GetValue<int>(SegmentColumns.EncodingType),
            RowCount = record.GetValue<int>(SegmentColumns.RowCount),
            OnDiskSize = record.GetValue<long>(SegmentColumns.OnDiskSize),

            HasNulls = (status & StatusHasNullsFlag) != 0,
            NullValue = record.GetValue<long?>(SegmentColumns.NullValue),

            BaseId = record.GetValue<long>(SegmentColumns.BaseId),
            Magnitude = record.GetValue<double>(SegmentColumns.Magnitude),

            MinDataId = record.GetValue<long>(SegmentColumns.MinDataId),
            MaxDataId = record.GetValue<long>(SegmentColumns.MaxDataId),

            MinDeepData = record.GetValue<byte[]?>(SegmentColumns.MinDeepData),
            MaxDeepData = record.GetValue<byte[]?>(SegmentColumns.MaxDeepData),

            CollationId = record.GetValue<int?>(SegmentColumns.CollationId),

            PrimaryDictionaryId = record.GetValue<int>(SegmentColumns.PrimaryDictionaryId),
            SecondaryDictionaryId = record.GetValue<int>(SegmentColumns.SecondaryDictionaryId),

            DataPointer = DecodeLobPointer(
                record.GetValue<byte[]?>(SegmentColumns.DataPtr)),

            Status = status,

            ContainerId = record.GetValue<short>(SegmentColumns.ContainerId),
            BloomFilterMetadata = record.GetValue<long>(SegmentColumns.BloomFilterMetadata),
            BloomFilterPointer = DecodeLobPointer(record.GetValue<byte[]?>(SegmentColumns.BloomFilterDataPtr)),

            UnmappedFields = CollectUnmapped(record, SegmentColumns.Known)
        };

        return segment;
    }

    private static SegmentDictionary MapDictionary(Record record)
    {
        return new SegmentDictionary
        {
            HobtId = record.GetValue<long>(DictionaryColumns.HobtId),
            ColumnId = record.GetValue<int>(DictionaryColumns.ColumnId),
            DictionaryId = record.GetValue<int>(DictionaryColumns.DictionaryId),
            Type = record.GetValue<int>(DictionaryColumns.Type),
            LastId = record.GetValue<int>(DictionaryColumns.LastId),
            EntryCount = record.GetValue<long>(DictionaryColumns.EntryCount),
            OnDiskSize = record.GetValue<long>(DictionaryColumns.OnDiskSize),
            Flags = record.GetValue<long>(DictionaryColumns.Flags),
            ContainerId = record.GetValue<short>(DictionaryColumns.ContainerId),

            DataPointer = DecodeLobPointer(record.GetValue<byte[]?>(DictionaryColumns.DataPtr)),

            UnmappedFields = CollectUnmapped(record, DictionaryColumns.Known)
        };
    }

    private static Dictionary<string, byte[]>? CollectUnmapped(Record record, HashSet<string> known)
    {
        Dictionary<string, byte[]>? unmapped = null;

        foreach (var field in record.Fields)
        {
            if (known.Contains(field.Name))
            {
                continue;
            }

            unmapped ??= [];
            unmapped[field.Name] = field.Data.ToArray();
        }

        return unmapped;
    }

    private static class SegmentColumns
    {
        public const string HobtId = "hobt_id";
        public const string SegmentId = "segment_id";
        public const string ColumnId = "column_id";
        public const string Version = "version";
        public const string EncodingType = "encoding_type";
        public const string RowCount = "row_count";
        public const string Status = "status";
        public const string BaseId = "base_id";
        public const string Magnitude = "magnitude";
        public const string PrimaryDictionaryId = "primary_dictionary_id";
        public const string SecondaryDictionaryId = "secondary_dictionary_id";
        public const string MinDataId = "min_data_id";
        public const string MaxDataId = "max_data_id";
        public const string NullValue = "null_value";
        public const string OnDiskSize = "on_disk_size";
        public const string CollationId = "collation_id";
        public const string MinDeepData = "min_deep_data";
        public const string MaxDeepData = "max_deep_data";
        public const string DataPtr = "data_ptr";
        public const string ContainerId = "container_id";
        public const string BloomFilterMetadata = "bloom_filter_md";
        public const string BloomFilterDataPtr = "bloom_filter_data_ptr";

        public static readonly HashSet<string> Known =
        [
            HobtId, SegmentId, ColumnId, Version, EncodingType, RowCount, Status,
            BaseId, Magnitude, PrimaryDictionaryId, SecondaryDictionaryId,
            MinDataId, MaxDataId, NullValue, OnDiskSize, CollationId,
            MinDeepData, MaxDeepData, DataPtr, ContainerId, BloomFilterMetadata, BloomFilterDataPtr
        ];
    }

    private static class RowGroupColumns
    {
        public const string SegmentId = "segment_id";
        public const string Version = "version";
        public const string DeltaStoreHobtId = "ds_hobtid";
        public const string RowCount = "row_count";
        public const string Status = "status";
        public const string Flags = "flags";
        public const string CompressedReason = "compressed_reason";
        public const string Generation = "generation";
        public const string CreatedTime = "created_time";
        public const string ClosedTime = "closed_time";
        public const string ContainerId = "container_id";
        public const string BlobId = "blob_id";
        public const string MetadataOffset = "metadata_offset";
        public const string MetadataSize = "metadata_size";
    }

    private static class DictionaryColumns
    {
        public const string HobtId = "hobt_id";
        public const string ColumnId = "column_id";
        public const string DictionaryId = "dictionary_id";
        public const string Version = "version";
        public const string Type = "type";
        public const string LastId = "last_id";
        public const string EntryCount = "entry_count";
        public const string OnDiskSize = "on_disk_size";
        public const string DataPtr = "data_ptr";
        public const string Flags = "flags";
        public const string ContainerId = "container_id";

        public static readonly HashSet<string> Known =
        [
            HobtId, ColumnId, DictionaryId, Version, Type, LastId, EntryCount, OnDiskSize, DataPtr,
            Flags, ContainerId
        ];
    }
}