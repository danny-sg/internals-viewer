using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Binding;

/// <summary>
/// Exposes the fields of a record as access path values
/// </summary>
/// <remarks>
/// The ordinal is the position of the field within the record. Resolving a column name to an
/// ordinal is the responsibility of whatever builds the predicate, so that the lookup happens
/// once rather than for every row.
/// </remarks>
public sealed class RecordRowValueSource(IRecord record) : IRowValueSource
{
    private IRecord Record { get; } = record;

    public AccessValue GetValue(int ordinal)
    {
        if (ordinal < 0 || ordinal >= Record.Fields.Count)
        {
            return AccessValue.Null;
        }

        return AccessValueFactory.FromField(Record.Fields[ordinal]);
    }
}
