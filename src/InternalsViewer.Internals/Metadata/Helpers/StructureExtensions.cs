using InternalsViewer.Internals.Metadata.Structures;
using System.Text;

namespace InternalsViewer.Internals.Metadata.Helpers;

public static class StructureExtensions
{
    public static string ToDetailString(this IndexStructure structure, int indent = 0)
    {
        var i = new string(' ', indent);

        var sb = new StringBuilder();
        sb.AppendLine($"{i}Index Structure");
        sb.AppendLine($"{i}---------------");

        sb.AppendLine();
        sb.AppendLine($"{i}Allocation Unit Id: {structure.AllocationUnitId}");
        sb.AppendLine();

        sb.AppendLine($"{i}Index Type:   {structure.IndexType}");
        sb.AppendLine();
        sb.AppendLine($"{i}Partition Id: {structure.PartitionId}");
        sb.AppendLine($"{i}Object Id:    {structure.ObjectId}");
        sb.AppendLine($"{i}Index Id:     {structure.IndexId}");

        if (structure.BaseIndexStructure != null)
        {
            sb.AppendLine($"{i}Base Index:");
            sb.AppendLine();
            sb.AppendLine(structure.BaseIndexStructure.ToDetailString(4));
        }

        sb.AppendLine();
        sb.AppendLine($"{i}Columns:");

        var maxColumnNameLength = structure.Columns.Max(c => c.ColumnName.Length as int?) ?? 0;

        var columnNameTitle = "Name".PadRight(maxColumnNameLength);

        sb.AppendLine($"{i}  Column Id | {columnNameTitle} | Type            | Length | Precision | Scale | Leaf Offset | Node Offset | Is Dropped | Is Uniqueifier | ");
        sb.AppendLine($"{i}  ----------+{new string('-', maxColumnNameLength + 2)}+-----------------+--------+-----------+-------+-------------+-------------+------------+----------------+");

        foreach (var column in structure.Columns)
        {
            var columnName = column.ColumnName.PadRight(maxColumnNameLength);

            sb.AppendLine($"{i}  " +
                          $" {column.ColumnId,8} |" +
                          $" {columnName} |" +
                          $" {column.DataType,-15} |" +
                          $" {column.DataLength,-6} |" +
                          $" {column.Precision,-9} |" +
                          $" {column.Scale,-5} |" +
                          $" {column.LeafOffset,-11} |" +
                          $" {column.NodeOffset,-11} |" +
                          $" {column.IsDropped,-10} |" +
                          $" {column.IsUniqueifier,-14} |"
                          );
        }

        return sb.ToString();
    }
}
