using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Metadata.Internals;
using System.Data;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing index structure information from the metadata collection
/// </summary>
/// <remarks>
/// Equivalent to the following query:
/// 
///     <code>
///         SELECT au.auid                                          AS AllocationUnitId
///               ,p.rowsetid                                       AS PartitionId
///               ,p.idmajor                                        AS ObjectId
///               ,p.idminor                                        AS IndexId
///               ,c.name                                           AS ColumnName
///               ,rsc.status
///               ,rsc.ti
///               ,CONVERT(SMALLINT
///                       ,CONVERT(BINARY(2), rsc.offset & 0xffff)) AS LeafOffset
///               ,rsc.status & 2                                   AS IsDropped
///               ,rsc.status & 16                                  AS IsUniqeifier
///         FROM   sys.sysallocunits au
///                INNER JOIN sys.sysrowsets p  ON au.ownerid = p.rowsetid
///                INNER JOIN sys.sysrscols rsc ON rsc.rsid = p.rowsetid
///                LEFT JOIN sys.sysiscols isc  ON isc.idmajor = p.idmajor
///                                                AND isc.idminor = p.idminor
///                                                AND isc.intprop = rsc.rscolid
///                LEFT JOIN sys.syscolpars c ON c.id = p.idmajor
///                                              AND c.colid = rsc.rscolid
///         WHERE  au.auid = 72057594059096064
///     </code>
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

        var parentIndex = metadata.Indexes
                                  .FirstOrDefault(i => i.ObjectId == rowSet.ObjectId
                                                  && i.IndexId <= 1);

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
        structure.IndexType = index.IndexType;

        structure.ParentIndexType = parentIndex?.IndexType ?? index.IndexType;

        var columnLayouts = metadata.ColumnLayouts.Where(c => c.PartitionId == rowSet.RowSetId).ToList();

        var columns = metadata.Columns.Where(c => c.ObjectId == rowSet.ObjectId).ToList();

        structure.Columns.AddRange(columnLayouts.Select(s =>
        {
            var column = columns.FirstOrDefault(c => c.ColumnId == s.ColumnId);

            // Get the index column for the column, or if that is not found get the clustered index column
            var indexColumn = metadata.IndexColumns
                                      .FirstOrDefault(c => c.ObjectId == rowSet.ObjectId
                                                           && c.IndexId == rowSet.IndexId
                                                           && c.ColumnId == s.ColumnId)
                           ?? metadata.IndexColumns
                                      .FirstOrDefault(c => c.ObjectId == rowSet.ObjectId
                                                           && c.IndexId == 1
                                                           && c.ColumnId == s.ColumnId);

            // The 2nd bit of the status field indicates if the column has been dropped
            var isDropped = Convert.ToBoolean(s.Status & 2);

            // The Offset field is a 4 byte integer, the first 2 bytes represent the leaf offset (offset in a leaf index page), the second
            // 2 bytes represent the node offset (offset in a node/non-leaf index page).
            var leafOffset = (short)(s.Offset & 0xffff);
            var nodeOffset = (short)(s.Offset >> 16);

            var typeInfo = s.TypeInfo.ToTypeInfo();

            var isUniqueifier = IsUniqueifier(s.Status, structure.IndexType, typeInfo.DataType, leafOffset);

            // The 5th bit of the status field indicates if the column is an include column
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

            var isKey = indexColumn != null && !isIncludeColumn && !isUniqueifier;

            var result = new IndexColumnStructure
            {
                ColumnId = s.ColumnId,
                ColumnName = name,
                DataType = typeInfo.DataType,
                LeafOffset = leafOffset,
                NodeOffset = nodeOffset,
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
