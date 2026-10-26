using System;
using System.Linq;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal sealed class BatchRowSource : IResultRowSource
{
    public const int FixedColumnCount = 2;

    public BatchRowSource(BatchState state, int vectorId, Func<BatchVector, int, object?> getSlotCell)
    {
        State = state;

        VectorId = vectorId;

        GetSlotCell = getSlotCell;

        Selections = state.GetSelectionIndexes();
    }

    public BatchState State { get; private set; }

    public int VectorId { get; }

    public int FieldCount => FixedColumnCount + State.Vectors.Count;

    private Func<BatchVector, int, object?> GetSlotCell { get; }

    private int[] Selections { get; set; }

    public bool IsSameLayout(BatchState state, int vectorId)
        => vectorId == VectorId
           && state.RowCount == State.RowCount
           && state.Vectors.Select(v => v.IsPure).SequenceEqual(State.Vectors.Select(v => v.IsPure));

    public void SetState(BatchState state)
    {
        State = state;

        Selections = state.GetSelectionIndexes();
    }

    public object? Value(int row, int ordinal)
    {
        if (ordinal == 0)
        {
            return row;
        }

        if (ordinal == 1)
        {
            return row < Selections.Length && Selections[row] >= 0 ? Selections[row] : null;
        }

        var vector = State.Vectors[ordinal - FixedColumnCount];

        return row < vector.Slots.Length && vector.Known[row] ? GetSlotCell(vector, row) : null;
    }
}
