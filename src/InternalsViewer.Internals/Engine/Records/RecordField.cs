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

    /// <summary>
    /// Indicates the field holds a NULL rather than an empty value
    /// </summary>
    /// <remarks>
    /// Set when the record is loaded. An empty <see cref="Data"/> is not enough on its own to
    /// identify a NULL, as a zero length string is also stored with no data.
    /// </remarks>
    public bool IsNull { get; set; }

    /// <summary>
    /// Typed representation of the field value
    /// </summary>
    /// <remarks>
    /// The typed counterpart of <see cref="Value"/>. Each record format stores values differently,
    /// so the decoding is left to the derived field.
    /// </remarks>
    public abstract T? GetValue<T>();
}
