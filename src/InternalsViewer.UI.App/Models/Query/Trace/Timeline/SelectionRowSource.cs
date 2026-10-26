using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal sealed class SelectionRowSource(BatchState state, ulong blockAddress) : IResultRowSource
{
    private const int EntrySize = sizeof(ushort);

    public BatchState State { get; private set; } = state;

    public int FieldCount => 4;

    public void SetState(BatchState value) => State = value;

    public object? Value(int row, int ordinal)
    {
        var entry = State.Selection[row];

        return ordinal switch
        {
            0 => row,
            1 => $"0x{State.SelectionAddress - blockAddress + (ulong)(row * EntrySize):X}",
            2 => (int)entry,
            _ => $"{entry & 0xFF:X2} {entry >> 8:X2}"
        };
    }
}
