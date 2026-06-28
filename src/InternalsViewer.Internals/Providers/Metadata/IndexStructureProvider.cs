using System.Data;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing index structure information from the metadata collection
/// </summary>
public static class IndexStructureProvider
{
    /// <summary>
    /// Gets the index structure for the specified allocation unit, using the database-level cache.
    /// </summary>
    public static IndexStructure GetIndexStructure(DatabaseSource database, long allocationUnitId)
    {
        if (database.IndexStructures.TryGetValue(allocationUnitId, out var cached))
        {
            return cached;
        }

        var structure = GetIndexStructure(database.Metadata, allocationUnitId);

        database.IndexStructures[allocationUnitId] = structure;

        return structure;
    }

    /// <summary>
    /// Gets the index structure for the specified allocation unit
    /// </summary>
    /// <remarks>
    /// Index structures are more complicated than the table structures as the metadata isn't as clear.
    /// 
    /// The physical structure of an index is defined in sys.sysrscols (rowset columns). sys.sysiscols (index columns) maps between the 
    /// index column and the table column, but there can be several other 'hidden' columns:
    ///
    ///     - Clustered indexes will include a uniqueifier column if required
    ///     - Non-clustered indexes on a clustered table will include the clustered index key columns (and the uniqueifier column if 
    ///       required)
    ///     - Non-clustered indexes on a heap will include a row identifier (RID)
    ///     
    /// The hidden columns are defined in the physical structure, but not in the logical structure so they have to be identified via 
    /// assumptions and patterns.
    ///     
    /// The method <see cref="IsUniqueifier(int, IndexType, SqlDbType, short)"/> identifies if a column is a uniqueifier.
    /// 
    /// RID columns will be a binary(8) column and are located in a row as the first fixed length column. This can only be confirmed when
    /// the record is decoded as the pminlen (fixed length data size) is only available in a page header.
    /// 
    /// Mapping from the metadata is as follows:
    /// 
    /// Allocation Unit Id --> Allocation Unit (Partition Id) 
    ///                          -> Partition 
    ///                                  -> Index (via Object Id, Index Id) 
    ///                                      -> Index Columns (via Object Id, Index Id)
    ///                                  -> Columns (via Object Id)
    ///                          -> Layout (via Partition Id)
    /// </remarks>
    public static IndexStructure GetIndexStructure(InternalMetadata metadata, long allocationUnitId)
    {
        var structure = new IndexStructure(allocationUnitId);

        if (!metadata.AllocationUnits.TryGetValue(allocationUnitId, out var allocationUnit))
        {
            throw new ArgumentException($"Allocation unit {allocationUnitId} not found");
        }

        if (!metadata.RowSets.TryGetValue(allocationUnit.ContainerId, out var rowSet))
        {
            throw new ArgumentException($"Row set {allocationUnit.ContainerId} not found");
        }

        var index = metadata.Indexes[rowSet.ObjectId]
                            .FirstOrDefault(i => i.IndexId == rowSet.IndexId);

        if (index == null)
        {
            throw new ArgumentException($"Index - Object Id: {rowSet.ObjectId}/ Index Id: {rowSet.IndexId} not found");
        }

        var parentAllocationUnitId = GetParentAllocationUnitId(rowSet.ObjectId, rowSet.PartitionNumber, metadata);

        if (parentAllocationUnitId.HasValue)
        {
            var baseStructure = TableStructureProvider.GetTableStructure(metadata, parentAllocationUnitId.Value);

            if (baseStructure.IndexType != IndexType.Heap)
            {
                structure.TableStructure = baseStructure;
            }
        }

        structure.IndexId = index.IndexId;
        structure.ObjectId = rowSet.ObjectId;
        structure.PartitionId = rowSet.RowSetId;

        structure.IsUnique = Convert.ToBoolean(index.Status & 0x8);
        structure.HasFilter = Convert.ToBoolean(index.Status & 0x20000);
        structure.IndexType = index.IndexType;

        if (structure.IndexType == IndexType.Clustered)
        {
            structure.Columns = GetClusteredIndexColumns(structure);
        }
        else
        {
            structure.Columns = GetNonClusteredIndexColumns(structure, metadata);
        }

        return structure;
    }

    /// <summary>
    /// Gets the clustered index columns
    /// </summary>
    /// <remarks>
    /// The clustered index structure is pretty straight forward as the table structure and index structure are defined in the same place.
    /// 
    /// The underlying table structure is used as the source.
    /// 
    /// The b-tree structure is defined in node offset, the data structure is defined in leaf offset although this is not relevant to 
    /// indexes as the data will be on data pages rather than index pages.
    /// </remarks>
    private static List<IndexColumnStructure> GetClusteredIndexColumns(IndexStructure structure)
    {
        if (structure.TableStructure == null)
        {
            return new List<IndexColumnStructure>();
        }

        return structure.TableStructure
                        .Columns
                        .Where(c => c.IsKey || c.IsUniqueifier)
                        .Select(s => new IndexColumnStructure
                        {
                            ColumnId = s.ColumnId,
                            ColumnName = s.ColumnName,
                            DataType = s.DataType,
                            LeafOffset = s.LeafOffset,
                            NodeOffset = s.NodeOffset,
                            Precision = s.Precision,
                            DataLength = s.DataLength,
                            Scale = s.Scale,
                            IsDropped = s.IsDropped,
                            IsUniqueifier = s.IsUniqueifier,
                            IsSparse = s.IsSparse,
                            NullBitIndex = s.NullBitIndex,
                            BitPosition = s.BitPosition,
                            IsIncludeColumn = false,
                            IndexColumnId = 0,
                            IsKey = s.IsKey
                        })
                        .ToList();
    }

    /// <summary>
    /// Gets the non-clustered index columns
    /// </summary>
    /// <remarks>
    /// Non-clustered indexes will be on a clustered table or a heap.
    /// </remarks>
    private static List<IndexColumnStructure> GetNonClusteredIndexColumns(IndexStructure structure, InternalMetadata metadata)
    {
        // Index physical structure
        var columnLayouts = metadata.ColumnLayouts[structure.PartitionId].ToList();

        // Table columns keyed by ColumnId for O(1) lookup
        var columns = metadata.Columns[structure.ObjectId].ToDictionary(c => c.ColumnId);

        // Index columns keyed by IndexColumnId for O(1) lookup
        var indexColumns = metadata.IndexColumns[(structure.ObjectId, structure.IndexId)]
                                   .ToDictionary(c => c.IndexColumnId);

        var keyStack = new List<ColumnStructure>();

        if (structure.TableStructure != null)
        {
            keyStack.AddRange(structure.TableStructure.Columns.Where(c => c.IsKey).OrderBy(o => o.ColumnId));
        }

        var result = columnLayouts.Select(layout => ToIndexColumnStructure(layout, structure, indexColumns, columns, keyStack)).ToList();

        return result;
    }

    private static IndexColumnStructure ToIndexColumnStructure(InternalColumnLayout layout,
                                                               IndexStructure structure,
                                                               Dictionary<int, InternalIndexColumn> indexColumns,
                                                               Dictionary<int, InternalColumn> columns,
                                                               List<ColumnStructure> keyStack)
    {
        var leafOffset = (short)(layout.Offset & 0xffff);
        var nodeOffset = (short)(layout.Offset >> 16);

        var typeInfo = layout.TypeInfo.ToTypeInfo();

        // If the index is explicitly defined on a column there should be a matching index column mapped via Index Column Id
        indexColumns.TryGetValue(layout.ColumnId, out var indexColumn);

        // An index column will map to a table column
        InternalColumn? column = null;
        if (indexColumn != null)
        {
            columns.TryGetValue(indexColumn.ColumnId, out column);
        }

        // Check if the column is a key column and remove it from the available key columns in the stack
        var key = keyStack.FirstOrDefault(f => f.ColumnId == indexColumn?.ColumnId);

        if (key != null)
        {
            keyStack.Remove(key);
        }

        // If there is no index column found assume it's the key and take the next available key column from the stack
        ColumnStructure? keyColumn = null;

        if (column == null)
        {
            keyColumn = keyStack.Pop(k => k.DataType == typeInfo.DataType);
        }

        // The 2nd bit of the status field indicates if the column has been dropped
        var isDropped = Convert.ToBoolean(layout.Status & 2);

        var isUniqueifier = IsUniqueifier(layout.Status, structure.IndexType, typeInfo.DataType, leafOffset);

        // The 5th bit of the status field indicates if the column is an include column
        var isIncludeColumn = Convert.ToBoolean(indexColumn?.Status & 0x10);

        string name;

        if (isDropped)
        {
            name = "(DROPPED)";
        }
        else if (isUniqueifier)
        {
            name = "UNIQUIFIER";
        }
        else
        {
            // First choice is the mapped index column, then the table column, then "Unknown"
            name = column?.Name ?? keyColumn?.ColumnName ?? $"Unknown column: {layout.ColumnId}";
        }

        // Is Key is defined by if the column has been found IN the index columns list or if the table column is a key column
        var isKey = indexColumn?.KeyOrdinal > 0 || keyColumn?.IsKey == true;

        var columnStructure = new IndexColumnStructure
        {
            ColumnId = (ushort)layout.ColumnId,
            ColumnName = name,
            DataType = typeInfo.DataType,
            LeafOffset = leafOffset,
            NodeOffset = nodeOffset,
            Precision = typeInfo.Precision,
            DataLength = typeInfo.MaxLength,
            Scale = typeInfo.Scale,
            IsDropped = isDropped,
            IsUniqueifier = isUniqueifier,
            IsSparse = (layout.Status & 256) != 0,
            NullBitIndex = (ushort)(layout.NullBit & 0xffff),
            BitPosition = (byte)layout.BitPosition,
            IsIncludeColumn = isIncludeColumn,
            IndexColumnId = indexColumn?.IndexColumnId ?? 0,
            IsKey = isKey,
            IsIndexKey = indexColumn?.KeyOrdinal > 0
        };

        return columnStructure;
    }

    /// <summary>
    /// Get the parent allocation unit id based on an Object Id/Partition Number
    /// </summary>
    private static long? GetParentAllocationUnitId(int objectId, int partitionNumber, InternalMetadata metadata)
    {
        var rowSet = metadata.RowSets.Values
                             .FirstOrDefault(p => p.ObjectId == objectId
                                             && p.PartitionNumber == partitionNumber
                                             && p.IndexId <= 1);

        var allocationUnit = metadata.AllocationUnits.Values
                                     .FirstOrDefault(a => a.ContainerId == rowSet?.RowSetId);

        return allocationUnit?.AllocationUnitId;
    }

    /// <summary>
    /// If the column is a Uniqueifier
    /// </summary>
    /// <remarks>
    /// A uniqueifier is a hidden column added to a clustered index to make every row unique, so it's own b-tree index and other indexes
    /// can navigate to the specific row.
    /// 
    /// Uniqueifier is indicated in sys.sysrscols.status for the column as the 5th bit being set (16 = 0b10000).
    /// 
    /// The status value for uniqueifier is not set for non-clustered indexes. Mark S. Rasmussen identified a pattern for determining the
    /// uniqueifier status for non-clustered indexes -
    ///     <see href="https://improve.dk/determining-the-uniquifier-column-ordinal-for-clustered-and-nonclustered-indexes/"/>
    /// </remarks>
    private static bool IsUniqueifier(int status, IndexType indexType, SqlDbType dataType, short leafOffset)
    {
        return Convert.ToBoolean(status & 16)
               || (indexType == IndexType.NonClustered && dataType == SqlDbType.Int && leafOffset < 0);
    }
}
