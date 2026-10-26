using System;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal sealed class VectorArrayRowSource(BatchState state, ulong blockAddress, Action<ulong> open) : IResultRowSource
{
    public BatchState State { get; private set; } = state;

    public int FieldCount => 5;

    public void SetState(BatchState value) => State = value;

    public object? Value(int row, int ordinal)
    {
        var vector = State.Vectors[row];

        return ordinal switch
        {
            0 => vector.Id,
            1 => $"0x{State.VectorArrayAddress - blockAddress + (ulong)(row * BatchReader.VectorEntrySize):X}",
            2 => GetLink(vector.Buffer),
            3 => GetLink(vector.CurrentDeepPage),
            _ => GetLink(vector.FirstDeepPage)
        };
    }

    private ActionLinks? GetLink(ulong address)
        => address == 0 ? null : new ActionLinks([new ActionLink($"0x{address:X16}", () => open(address))]);
}
