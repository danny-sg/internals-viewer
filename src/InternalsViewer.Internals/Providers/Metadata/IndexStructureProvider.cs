using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using System.Data;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing index structure information from the metadata collection
/// </summary>
/// <remarks>
/// Equivalent to the following query:
/// 
/// 
/// </remarks>
public class IndexStructureProvider
{
    /// <summary>
    /// Gets the structure for the specified allocation unit
    /// </summary>
    public static IndexStructure GetIndexStructure(InternalMetadata metadata, long allocationUnitId)
    {
        var structure = new IndexStructure(allocationUnitId);

        var allocationUnit = metadata.AllocationUnits
                                     .FirstOrDefault(a => a.AllocationUnitId == allocationUnitId);

        if (allocationUnit == null)
        {
            throw new ArgumentException($"Allocation unit {allocationUnitId} not found");
        }

        var rowSet = metadata.RowSets
                             .FirstOrDefault(p => p.RowSetId == allocationUnit.ContainerId);

        if (rowSet == null)
        {
            throw new ArgumentException($"Row set {allocationUnit.ContainerId} not found");
        }

        var index = metadata.Indexes.FirstOrDefault(i => i.ObjectId == rowSet.ObjectId
                                                         && i.IndexId == rowSet.IndexId);

        if (index == null)
        {
            throw new ArgumentException($"Index - Object Id: {rowSet.ObjectId}/ Index Id: {rowSet.IndexId} not found");
        }

        structure.IndexId = index.IndexId;
        structure.ObjectId = rowSet.ObjectId;

        structure.IsUnique = Convert.ToBoolean(index.Status & 0x8);
        structure.HasFilter = Convert.ToBoolean(index.Status & 0x20000);
        structure.IndexType = (IndexType)index.IndexType;

        var columnLayouts = metadata.ColumnLayouts.Where(c => c.PartitionId == rowSet.RowSetId).ToList();

        var columns = metadata.Columns.Where(c => c.ObjectId == rowSet.ObjectId).ToList();

        structure.Columns.AddRange(columnLayouts.Select(s =>
        {
            var column = columns.FirstOrDefault(c => c.ColumnId == s.ColumnId);

            var indexColumn = metadata.IndexColumns
                                      .FirstOrDefault(c => c.ObjectId == rowSet.ObjectId
                                                      && c.IndexId == rowSet.IndexId
                                                      && c.ColumnId == s.ColumnId);

            var isDropped = Convert.ToBoolean(s.Status & 2);

            var leafOffset = (short)s.Offset;

            var typeInfo = s.TypeInfo.ToTypeInfo();

            var isUniqueifier = IsUniqueifier(s.Status, structure.IndexType, typeInfo.DataType, leafOffset);

            var isIncludeColumn = Convert.ToBoolean(indexColumn?.Status & 0x10);

            string name;

            if (isDropped)
            {
                name = "(Dropped)";
            }
            else if (isUniqueifier)
            {
                name = "(Uniqueifer)";
            }
            else
            {
                name = column?.Name ?? "Unknown";
            }

            var isKey = indexColumn == null && !isIncludeColumn && !isUniqueifier;

            var result = new IndexColumnStructure
            {
                ColumnId = s.ColumnId,
                ColumnName = name,
                DataType = typeInfo.DataType,
                LeafOffset = (short)s.Offset,
                Precision = typeInfo.Precision,
                DataLength = typeInfo.MaxLength,
                Scale = typeInfo.Scale,
                IsDropped = isDropped,
                IsUniqueifier = isUniqueifier,
                IsSparse = (s.Status & 256) != 0,
                NullBitIndex = (short)(s.NullBit & 0xffff),
                BitPosition = s.BitPosition,
                IsIncludeColumn = isIncludeColumn,
                IndexColumnId = indexColumn?.IndexColumnId ?? 0,
                IsKey = isKey
            };

            return result;
        }));

        return structure;
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
