using System;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal sealed class VectorRowSource : IResultRowSource
{
    private const int SlotSize = sizeof(ulong);

    public VectorRowSource(BatchState state, BatchVector vector, ulong blockAddress, Func<BatchVector, int, object?> getSlotCell)
    {
        Vector = vector;

        BlockAddress = blockAddress;

        GetSlotCell = getSlotCell;

        Selections = state.GetSelectionIndexes();
    }

    public BatchVector Vector { get; private set; }

    public int FieldCount => 4;

    private ulong BlockAddress { get; }

    private Func<BatchVector, int, object?> GetSlotCell { get; }

    private int[] Selections { get; set; }

    public bool IsSameLayout(BatchVector vector) => vector.Id == Vector.Id && vector.Slots.Length == Vector.Slots.Length;

    public void SetState(BatchState state, BatchVector vector)
    {
        Vector = vector;

        Selections = state.GetSelectionIndexes();
    }

    public object? Value(int row, int ordinal) => ordinal switch
    {
        0 => row,
        1 => row < Selections.Length && Selections[row] >= 0 ? Selections[row] : null,
        2 => $"0x{Vector.Buffer - BlockAddress + (ulong)(row * SlotSize):X}",
        _ => row < Vector.Slots.Length && Vector.Known[row] ? GetSlotCell(Vector, row) : null
    };
}
