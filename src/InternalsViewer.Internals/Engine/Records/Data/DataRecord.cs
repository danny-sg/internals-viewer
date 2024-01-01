using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records.Data;

public class DataRecord : Record
{
    public SparseVector? SparseVector { get; set; }

    [DataStructureItem(DataStructureItemType.StatusBitsB)]
    public string StatusBitsBDescription => "";

    [DataStructureItem(DataStructureItemType.ForwardingStub)]
    public RowIdentifier ForwardingStub { get; set; }

    public T? GetValue<T>(string columnName)
    {
        var field = Fields.FirstOrDefault(f => f.Name.ToLower() == columnName.ToLower());

        if (field == null)
        {
            throw new ArgumentException($"Column {columnName} not found");
        }

        if(field.Data.Length == 0)
        {
            return default;
        }

        return DataConverter.GetValue<T>(field.Data,
                                         field.ColumnStructure.DataType,
                                         field.ColumnStructure.Precision,
                                         field.ColumnStructure.Scale);
    }
}