using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Metadata.Internals;

namespace InternalsViewer.Internals.Providers.Metadata;

public class TableStructureProvider
{
    public static TableStructure GetTableStructure(InternalMetadata metadata, long allocationUnitId)
    {
        var structure = new TableStructure(allocationUnitId);

        var allocationUnit = metadata.AllocationUnits
                                     .First(a => a.AllocationUnitId == allocationUnitId);

        var rowSet = metadata.RowSets
                             .First(p => p.RowSetId == allocationUnit.ContainerId);

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
                NullBit = (short)(s.NullBit & 0xffff)
            };

            return result;
        }));

        return structure;
    }
}
