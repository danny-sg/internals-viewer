using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins;

public sealed class JoinInput(IIterator iterator) : IJoinInput
{
    public IIterator Iterator { get; } = iterator;

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
