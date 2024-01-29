using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records;

public abstract class RecordField(ColumnStructure columnStructure) : Field
{
    public ColumnStructure ColumnStructure { get; } = columnStructure;

    /// <summary>
    /// Length of the field (in bytes)
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Offset of the field in the row
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Raw data for the field
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public string Name => ColumnStructure.ColumnName;

    /// <summary>
    /// String representation of the field value
    /// </summary>
    public abstract string Value { get; }
}