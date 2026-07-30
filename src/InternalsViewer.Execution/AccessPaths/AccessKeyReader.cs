using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.AccessPaths;

internal static class AccessKeyReader
{
    public static AccessKey GetKey(IRecord record, IndexStructure indexStructure)
    {
        var columns = indexStructure.IndexKeyColumns;

        var values = new AccessValue[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            values[index] = CreateValue(record, columns[index]);
        }

        return AccessKey.Create(values);
    }

    public static int ComparePrefix(in AccessKey key, in AccessKey target, int width, IndexStructure indexStructure)
    {
        var length = Math.Min(width, Math.Min(key.Count, target.Count));

        var keyColumns = indexStructure.IndexKeyColumns;

        for (var index = 0; index < length; index++)
        {
            var result = AccessValueComparer.Compare(key[index], target[index]);

            if (result != 0)
            {
                var isDescending = index < keyColumns.Count && keyColumns[index].IsDescending;

                return isDescending ? -result : result;
            }
        }

        return 0;
    }

    private static AccessValue CreateValue(IRecord record, IndexColumnStructure keyColumn)
    {
        foreach (var field in record.Fields)
        {
            if (field.ColumnStructure.ColumnId == keyColumn.ColumnId)
            {
                return AccessValueFactory.FromField(field).WithColumnName(keyColumn.ColumnName);
            }
        }

        return AccessValue.FromNull(keyColumn.DataType).WithColumnName(keyColumn.ColumnName);
    }
}
