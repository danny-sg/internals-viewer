using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records;

public abstract class RecordField(ColumnStructure columnStructure) : Field
{
    public ColumnStructure ColumnStructure { get; } = columnStructure;

    /// <summary>
    /// Length of the field (in bytes)
    /// </summary>
    public ushort Length { get; set; }

    /// <summary>
    /// Offset of the field in the row
    /// </summary>
    public ushort Offset { get; set; }

    /// <summary>
    /// Raw data for the field
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; set; } = ReadOnlyMemory<byte>.Empty;

    public string Name => ColumnStructure.ColumnName;

    /// <summary>
    /// String representation of the field value
    /// </summary>
    public abstract string Value { get; }
}