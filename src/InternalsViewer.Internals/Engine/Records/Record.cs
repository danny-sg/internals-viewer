using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Engine.Records;

/// <summary>
/// Database Record Structure
/// </summary>
public abstract class Record : DataStructure, IRecord
{
    public int Slot { get; set; }

    public ushort Offset { get; set; }

    public List<RecordField> Fields { get; } = [];

    public RecordField[] FieldsArray => [.. Fields];

    [DataStructureItem(ItemType.ColumnCount)]
    public short ColumnCount { get; set; }

    /// <inheritdoc />
    public abstract bool IsGhost { get; }

    /// <summary>
    /// Gets the typed value of a named column
    /// </summary>
    /// <remarks>
    /// Resolving by name is a linear search, so repeated access over many records should hold on
    /// to the <see cref="RecordField"/> and call <see cref="RecordField.GetValue{T}"/> directly.
    /// </remarks>
    public T? GetValue<T>(string columnName)
    {
        var field = Fields.FirstOrDefault(
            f => string.Equals(f.Name, columnName, StringComparison.CurrentCultureIgnoreCase));

        if (field == null)
        {
            throw new ArgumentException($"Column {columnName} not found");
        }

        return field.GetValue<T>();
    }
}
