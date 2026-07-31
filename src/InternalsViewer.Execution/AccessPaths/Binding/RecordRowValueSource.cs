using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Binding;

/// <summary>
/// Exposes the fields of a record as access path values
/// </summary>
public sealed class RecordRowValueSource(IRecord record) : IRowValueSource
{
    private IRecord Record { get; } = record;

    public AccessValue GetValue(int ordinal, string? columnName = null)
    {
        if (ordinal >= 0 && ordinal < Record.Fields.Count)
        {
            return AccessValueFactory.FromField(Record.Fields[ordinal]);
        }

        if (columnName is not null)
        {
            var field = Record.Fields
                              .FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName,
                                                                 columnName,
                                                                 StringComparison.OrdinalIgnoreCase));

            if (field is not null)
            {
                return AccessValueFactory.FromField(field);
            }
        }

        return AccessValue.Null;
    }
}
