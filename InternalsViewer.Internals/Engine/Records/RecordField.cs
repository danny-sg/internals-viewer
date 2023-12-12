using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records;

/// <summary>
/// Record field
/// </summary>
public class RecordField(Column column) : Field
{
    public BlobField BlobInlineRoot { get; set; }

    public Column Column { get; set; } = column;

    public int LeafOffset => Column.LeafOffset;

    public int Length { get; set; }

    public int Offset { get; set; }

    public byte[] Data { get; set; }

    public int VariableOffset { get; set; }

    public bool Compressed { get; set; }

    public bool Sparse { get; set; }

    public string Name => Column.ColumnName;

    [DataStructureItem(DataStructureItemType.Value)]
    public string Value => DataConverter.BinaryToString(Data, Column.DataType, Column.Precision, Column.Scale);

    public override string ToString()
    {
        return string.Format("  Offset: {0, -4} Leaf Offset: {1, -4} Length: {2, -4} Field: {3, -30} Data type: {4, -10} Value: {5}",
            Offset,
            LeafOffset,
            Length,
            Column.ColumnName,
            Column.DataType,
            DataConverter.ToHexString(Data));
    }
}