using System;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records.Data;

public class DataRecord : Record
{
    public SparseVector? SparseVector { get; set; }

    [DataStructureItem(DataStructureItemType.StatusBitsB)]
    public string StatusBitsBDescription => "";

    [DataStructureItem(DataStructureItemType.ForwardingRecord)]
    public RowIdentifier ForwardingRecord { get; set; }

    public T? GetValue<T>(string columnName)
    {
        var field = Fields.FirstOrDefault(f => f.Name == columnName);

        if (field == null)
        {
            throw new ArgumentException($"Column {columnName} not found", nameof(columnName));
        }

        return DataConverter.GetValue<T>(field.Data,
                                         field.ColumnStructure.DataType,
                                         field.ColumnStructure.Precision,
                                         field.ColumnStructure.Scale);
    }
}