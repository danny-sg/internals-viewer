using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Binding;

/// <summary>
/// Exposes both rows of a candidate pair as one row, for a predicate a join applies after its keys have matched
/// </summary>
/// <remarks>
/// Columns are found by name, which is how a join residual names them. A side is only consulted when it actually carries the column, so a
/// column that is genuinely NULL on one side does not fall through and read the other side's value. Where both sides carry the name, which
/// the columns an equijoin matches on nearly always do, the side is settled by which operand asked, see <see cref="Reading"/>.
/// </remarks>
internal sealed class JoinRowValueSource(IRecord outerRecord, IRecord innerRecord) : IRowValueSource
{
    /// <summary>
    /// The pair read outer row first, which is where the left of a join's comparison is taken from
    /// </summary>
    public IRowValueSource FromOuter { get; } = new Reading(outerRecord, innerRecord);

    public IRowValueSource FromInner { get; } = new Reading(innerRecord, outerRecord);

    public AccessValue GetValue(int ordinal, string? columnName = null) => FromOuter.GetValue(ordinal, columnName);

    /// <summary>
    /// The pair read from one side first, falling back to the other for a column that side does not carry
    /// </summary>
    /// <remarks>
    /// A join residual relates the two sides it joined, so its operands name a column from each and each operand reads the pair from its
    /// own side. Which side asked first decides nothing unless both carry the name, and that is the case no other signal settles - a self
    /// join names both sides identically, and the table showplan qualified the column with is gone by the time the predicate is parsed.
    /// </remarks>
    private sealed class Reading(IRecord first, IRecord second) : IRowValueSource
    {
        private RecordRowValueSource First { get; } = new(first);

        private RecordRowValueSource Second { get; } = new(second);

        public AccessValue GetValue(int ordinal, string? columnName = null)
        {
            if (columnName is null)
            {
                return First.GetValue(ordinal);
            }

            if (Has(first, columnName))
            {
                return First.GetValue(-1, columnName);
            }

            return Has(second, columnName)
                ? Second.GetValue(-1, columnName)
                : AccessValue.Null;
        }

        private static bool Has(IRecord record, string columnName)
            => record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
    }
}
