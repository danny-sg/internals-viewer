using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

namespace InternalsViewer.Internals.Engine.Records.Data;

public sealed class DataRecord : FixedVarRecord
{
    public SparseVector? SparseVector { get; set; }

    [DataStructureItem(ItemType.StatusBitsB)]
    public string StatusBitsBDescription => string.Empty;

    [DataStructureItem(ItemType.ForwardingStub)]
    public RowIdentifier? ForwardingStub { get; set; }

    public RowIdentifier? RowIdentifier { get; set; }

    public T? GetValue<T>(string columnName)
    {
        var field = Fields.FirstOrDefault(
            f => string.Equals(f.Name, columnName, StringComparison.CurrentCultureIgnoreCase));

        if (field == null)
        {
            throw new ArgumentException($"Column {columnName} not found");
        }

        if (field.Data.IsEmpty)
        {
            return default;
        }

        return DataConverter.GetValue<T>(field.Data.ToArray(),
                                         field.ColumnStructure.DataType,
                                         field.ColumnStructure.Precision,
                                         field.ColumnStructure.Scale);
    }
}