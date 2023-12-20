using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Engine.Records;

/// <summary>
/// Record field
/// </summary>
public class RecordField(ColumnStructure columnStructure) : Field
{
    public BlobField? BlobInlineRoot { get; set; }

    public ColumnStructure ColumnStructure { get; set; } = columnStructure;

    public int LeafOffset => ColumnStructure.LeafOffset;

    public int Length { get; set; }

    public int Offset { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public int VariableOffset { get; set; }

    public bool Compressed { get; set; }

    public bool Sparse { get; set; }

    public string Name => ColumnStructure.ColumnName;

    [DataStructureItem(DataStructureItemType.Value)]
    public string Value => DataConverter.BinaryToString(Data, ColumnStructure.DataType, ColumnStructure.Precision, ColumnStructure.Scale);

    public override string ToString()
    {
        return string.Format("  Offset: {0, -4} Leaf Offset: {1, -4} Length: {2, -4} Field: {3, -30} Data type: {4, -10} Value: {5}",
                             Offset,
                             LeafOffset,
                             Length,
                             ColumnStructure.ColumnName,
                             ColumnStructure.DataType,
                             DataConverter.ToHexString(Data));
    }
}