using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.Interfaces.Services.Joins.Inputs;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins.Inputs;

/// <summary>
/// Base for a join input where a loop starts again for each row of the other side
/// </summary>
public abstract class RebindableJoinInput : JoinInput, IRebindableJoinInput
{
    public abstract bool FetchesDirectly { get; }

    private bool IsResidualChecked { get; set; }

    public abstract Task<AccessStep> RebindAsync(DatabaseSource database,
                                                 IRecord outerRecord,
                                                 int rebindNumber,
                                                 CancellationToken cancellationToken);

    /// <summary>
    /// Rejects a residual that reads a column only the outer row has, which the inner access path cannot see
    /// </summary>
    /// <remarks>
    /// Only the seek key carries outer values into a rebind. The residual is evaluated against the inner row on its own, so a column the
    /// inner does not have resolves to null, every comparison against it is unknown and the join quietly returns nothing rather than
    /// failing. A name both sides carry is left alone, because it resolves against the inner row and that is what a residual means.
    /// </remarks>
    protected void GuardResidual(AccessPredicate? residual, IRecord outerRecord, IReadOnlySet<string> innerColumns)
    {
        if (IsResidualChecked || residual is null || innerColumns.Count == 0)
        {
            return;
        }

        IsResidualChecked = true;

        var outerColumns = outerRecord.Fields
                                      .Select(f => f.ColumnStructure.ColumnName)
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referenced = PredicateColumns.Referenced(residual)
                                         .Where(c => outerColumns.Contains(c) && !innerColumns.Contains(c))
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();

        if (referenced.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"The residual reads {string.Join(", ", referenced.Select(c => $"'{c}'"))} from the outer "
                                            + "row, which a rebind cannot bind. Only the seek key carries outer values into the inner "
                                            + "access path, so a join predicate has to be expressed as a seek binding");
    }
}
