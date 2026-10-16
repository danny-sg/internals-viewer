using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class ColumnValueParser
{
    public const ulong SmallestAddress = 0x10000;

    public const RowBufferColumnFlags SlotUnreadable = RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable;

    private const ulong MaximumValueBytes = 8000;

    public static RowBufferColumn Create(int ordinal, RowBufferBinding binding)
        => new(ordinal, binding, 0, RowBufferColumnFlags.None, 0, []);

    public static int GetLength(ulong length) => (int)(uint)Math.Min(length, uint.MaxValue);

    public static RowBufferColumn ReadReference(ITraceReplay replay, ulong pointer, ulong length, RowBufferColumn column)
    {
        column = column with { Pointer = pointer };

        return PageReferenceParser.Parse(replay, pointer, length) is { } page
            ? Defer(column, page)
            : ReadBytes(replay, pointer, length, column);
    }

    public static RowBufferColumn ReadBytes(ITraceReplay replay, ulong address, ulong length, RowBufferColumn column)
    {
        var data = new byte[(int)Math.Min(length, MaximumValueBytes)];

        var found = address < SmallestAddress || data.Length == 0 ? 0 : replay.Read(address, data);

        var flags = column.Flags;

        if (found < data.Length)
        {
            flags |= RowBufferColumnFlags.Unreadable;
        }
        else if ((uint)found < (uint)column.Length)
        {
            flags |= RowBufferColumnFlags.Truncated;
        }

        return column with { Data = found == data.Length ? data : data[..found], Flags = flags };
    }

    public static RowBufferColumn Defer(RowBufferColumn column, DeferredValue value)
        => column with
        {
            Data = [],
            Flags = (column.Flags & ~RowBufferColumnFlags.Unreadable) | RowBufferColumnFlags.Deferred,
            Deferred = value
        };
}
