using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// Converts a decoded <see cref="RecordField"/> into a labelled <see cref="AccessValue"/>
/// </summary>
/// <remarks>
/// Field bytes are already positioned and typed by the record loader, so this factory only needs to
/// classify the data type into the storage kind an <see cref="AccessValue"/> understands.
/// </remarks>
public static class AccessValueFieldFactory
{
    public static AccessValue Create(RecordField field)
    {
        return AccessValueFactory.FromField(field).WithColumnName(field.ColumnStructure.ColumnName);
    }
}
