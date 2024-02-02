using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records;

/// <summary>
/// FixedVar Record Field
/// </summary>
public class FixedVarRecordField(ColumnStructure columnStructure) : RecordField(columnStructure)
{
    public BlobField? BlobInlineRoot { get; set; }

    public int VariableOffset { get; set; }

    public bool IsSparse { get; set; }

    public override string Value => DataConverter.BinaryToString(Data,
                                                                 ColumnStructure.DataType,
                                                                 ColumnStructure.Precision,
                                                                 ColumnStructure.Scale,
                                                                 ColumnStructure.BitPosition);

    public override string ToString()
    {
        return string.Format("  Offset: {0, -4} Leaf Offset: {1, -4} Length: {2, -4} Field: {3, -30} Data type: {4, -10} Value: {5}",
                             Offset,
                             ColumnStructure.LeafOffset,
                             Length,
                             ColumnStructure.ColumnName,
                             ColumnStructure.DataType,
                             StringHelpers.ToHexString(Data));
    }
}