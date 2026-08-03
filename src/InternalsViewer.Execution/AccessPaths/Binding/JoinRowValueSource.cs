using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Binding;

/// <summary>
/// Exposes both rows of a candidate pair as one row, for a predicate a join applies after its keys have matched
/// </summary>
/// <remarks>
/// Columns are found by name, which is how a join residual names them. A side is only consulted when it actually carries the column, so a
/// column that is genuinely NULL on one side does not fall through and read the other side's value. A self join, where both sides carry
/// the same names, cannot be told apart this way and resolves to the outer row.
/// </remarks>
public sealed class JoinRowValueSource(IRecord outerRecord, IRecord innerRecord) : IRowValueSource
{
    private RecordRowValueSource Outer { get; } = new(outerRecord);

    private RecordRowValueSource Inner { get; } = new(innerRecord);

    public AccessValue GetValue(int ordinal, string? columnName = null)
    {
        if (columnName is null)
        {
            return Outer.GetValue(ordinal);
        }

        if (Has(outerRecord, columnName))
        {
            return Outer.GetValue(-1, columnName);
        }

        return Has(innerRecord, columnName)
            ? Inner.GetValue(-1, columnName)
            : AccessValue.Null;
    }

    private static bool Has(IRecord record, string columnName)
        => record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
}
