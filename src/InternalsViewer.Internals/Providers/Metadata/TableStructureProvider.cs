using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Metadata.Internals;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing table structure information from the metadata collection
/// </summary>
public class TableStructureProvider
{
    /// <summary>
    /// Gets the table structure for the specified allocation unit
    /// </summary>
    public static TableStructure GetTableStructure(InternalMetadata metadata, long allocationUnitId)
    {
        var structure = new TableStructure(allocationUnitId);

        var allocationUnit = metadata.AllocationUnits
                                     .FirstOrDefault(a => a.AllocationUnitId == allocationUnitId);

        if(allocationUnit == null)
        {
            throw new ArgumentException($"Allocation unit {allocationUnitId} not found");
        }

        var rowSet = metadata.RowSets
                             .FirstOrDefault(p => p.RowSetId == allocationUnit.ContainerId);

        if(rowSet == null)
        {
            throw new ArgumentException($"Row set {allocationUnit.ContainerId} not found");
        }

        var columnLayouts = metadata.ColumnLayouts.Where(c => c.PartitionId == rowSet.RowSetId).ToList();

        var columns = metadata.Columns.Where(c => c.ObjectId == rowSet.ObjectId).ToList();

        structure.Columns.AddRange(columnLayouts.Select(s =>
        {
            var column = columns.FirstOrDefault(c => c.ColumnId == s.ColumnId);

            var isDropped = Convert.ToBoolean(s.Status & 2);
            var isUniqueifer = Convert.ToBoolean(s.Status & 16);   

            string name;

            if(isDropped)
            {
                name = "(Dropped)";
            }
            else if(isUniqueifer)
            {
                name = "(Uniqueifer)";
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
                LeafOffset = (short)(s.Offset & 0xffff),
                Precision = typeInfo.Precision,
                DataLength = typeInfo.MaxLength,
                Scale = typeInfo.Scale,
                IsDropped = isDropped,
                IsUniqueifer = isUniqueifer,
                IsSparse = (s.Status & 256) != 0,
                NullBit = (short)(s.NullBit & 0xffff),
                BitPosition = s.BitPosition
            };

            return result;
        }));

        return structure;
    }
}
