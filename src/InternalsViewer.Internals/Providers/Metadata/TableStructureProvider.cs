using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing table structure information from the metadata collection
/// </summary>
public class TableStructureProvider
{
    /// <summary>
    /// Gets the table structure for the specified allocation unit, using the database-level cache.
    /// </summary>
    public static TableStructure GetTableStructure(DatabaseSource database, long allocationUnitId)
    {
        if (database.TableStructures.TryGetValue(allocationUnitId, out var cached))
        {
            return cached;
        }

        var structure = GetTableStructure(database.Metadata, allocationUnitId);

        database.TableStructures[allocationUnitId] = structure;

        return structure;
    }

    /// <summary>
    /// Gets the table structure for the specified allocation unit from the metadata collection.
    /// </summary>
    public static TableStructure GetTableStructure(InternalMetadata metadata, long allocationUnitId)
    {
        var structure = new TableStructure(allocationUnitId);

        if (!metadata.AllocationUnits.TryGetValue(allocationUnitId, out var allocationUnit))
        {
            throw new ArgumentException($"Allocation unit {allocationUnitId} not found");
        }

        if (!metadata.RowSets.TryGetValue(allocationUnit.ContainerId, out var rowSet))
        {
            throw new ArgumentException($"Row set {allocationUnit.ContainerId} not found");
        }

        structure.IndexType = rowSet.IndexId == 0 ? IndexType.Heap : IndexType.Clustered;

        structure.CompressionType = rowSet.CompressionType;

        var columnLayouts = metadata.ColumnLayouts[rowSet.RowSetId].ToList();

        var columns = metadata.Columns[rowSet.ObjectId].ToDictionary(c => c.ColumnId);

        var indexColumnIds = metadata.IndexColumns[(rowSet.ObjectId, rowSet.IndexId)]
                                     .Select(c => c.ColumnId)
                                     .ToHashSet();

        structure.Columns.AddRange(columnLayouts.Select(s =>
        {
            columns.TryGetValue(s.ColumnId, out var column);

            var isDropped = Convert.ToBoolean(s.Status & 2);
            var isUniqueifer = Convert.ToBoolean(s.Status & 16);
            var isKey = indexColumnIds.Contains(s.ColumnId);

            structure.ObjectId = rowSet.ObjectId;
            structure.IndexId = rowSet.IndexId;
            structure.PartitionId = rowSet.RowSetId;

            /*
                The Offset field is a 4 byte integer, the first 2 bytes represent the leaf offset (offset in a leaf index page), the second
                2 bytes represent the node offset (offset in a node/non-leaf index page).
            */
            var leafOffset = (short)(s.Offset & 0xffff);
            var nodeOffset = (short)(s.Offset >> 16);

            string name;

            if (isDropped)
            {
                name = "(Dropped)";
            }
            else if (isUniqueifer)
            {
                name = "UNIQUIFIER";
            }
            else
            {
                name = column?.Name ?? "Unknown";
            }

            var typeInfo = s.TypeInfo.ToTypeInfo();

            var result = new ColumnStructure
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
                IsUniqueifier = isUniqueifer,
                IsSparse = (s.Status & 256) != 0,
                NullBitIndex = (short)(s.NullBit & 0xffff),
                BitPosition = s.BitPosition,
                IsKey = isKey
            };

            return result;
        }));

        return structure;
    }
}
