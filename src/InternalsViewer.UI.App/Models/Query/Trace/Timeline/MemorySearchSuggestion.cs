using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemorySearchSuggestion(string label, ulong address, int size, int block, TracePosition position)
{
    public string Label { get; } = label;

    public ulong Address { get; } = address;

    public int Size { get; } = size;

    public int Block { get; } = block;

    public TracePosition Position { get; } = position;

    public string Detail => $"0x{Address:X16}, {Size:N0} Bytes";
}
