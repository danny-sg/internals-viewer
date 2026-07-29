using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

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
