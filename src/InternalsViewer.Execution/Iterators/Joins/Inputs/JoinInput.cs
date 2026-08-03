using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators.Joins.Inputs;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins.Inputs;

/// <summary>
/// Base for a join input, holding the rows the join is still working with
/// </summary>
public abstract class JoinInput : IJoinInput
{
    public abstract IStepIterator Service { get; }

    public abstract AccessStrategy? Strategy { get; }

    public IReadOnlyList<JoinBufferRow> Buffer => Rows;

    private List<JoinBufferRow> Rows { get; } = [];

    public void Clear()
    {
        Rows.Clear();
    }

    /// <summary>
    /// Takes a row the input has just returned, dropping any the join has already finished with
    /// </summary>
    /// <remarks>
    /// A row that has been paired or passed over is no longer held by the join, so it goes as soon as the walk moves on. Rows of a matched
    /// group stay because they are replayed against any further rows carrying the same key.
    /// </remarks>
    public void Collect(IRecord record)
    {
        Rows.RemoveAll(r => r.State == JoinRowState.Finished);

        Hold(record, JoinRowState.Pending);
    }

    public void Hold(IRecord record, JoinRowState state)
    {
        Rows.Add(new JoinBufferRow(record, state));
    }

    public void MarkMatched(IRecord record)
    {
        MarkState(record, JoinRowState.Matched);
    }

    public void MarkState(IRecord record, JoinRowState state)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            if (ReferenceEquals(Rows[index].Record, record))
            {
                Rows[index] = Rows[index] with { State = state };

                return;
            }
        }
    }
}
