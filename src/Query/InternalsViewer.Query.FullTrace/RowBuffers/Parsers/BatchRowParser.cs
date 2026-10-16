using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class BatchRowParser
{
    private const int MaximumValues = 1024;

    public const ulong LayoutValueSize = 0x20;

    private const ulong LayoutStatusOffset = 0x00;

    private const ulong LayoutDataOffset = 0x08;

    private const ulong LayoutLengthOffset = 0x10;

    public static RowBufferColumn[]? Parse(ITraceReplay replay, ulong buffer, uint count)
    {
        if (buffer < ColumnValueParser.SmallestAddress || count == 0 || count > MaximumValues)
        {
            return null;
        }

        var columns = new RowBufferColumn[count];

        for (var index = 0; index < columns.Length; index++)
        {
            columns[index] = ParseValue(replay, index, buffer + (ulong)index * LayoutValueSize);
        }

        return columns;
    }

    private static RowBufferColumn ParseValue(ITraceReplay replay, int ordinal, ulong address)
    {
        var column = ColumnValueParser.Create(ordinal, RowBufferBinding.None);

        if (!replay.TryRead(address + LayoutStatusOffset, out byte status))
        {
            return column with { Flags = ColumnValueParser.SlotUnreadable };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        if (!replay.TryRead(address + LayoutDataOffset, out ulong value) || !replay.TryRead(address + LayoutLengthOffset, out ulong length))
        {
            return column with { Flags = ColumnValueParser.SlotUnreadable };
        }

        if (length == 0)
        {
            return column with { Length = sizeof(ulong), Data = BitConverter.GetBytes(value) };
        }

        column = column with
        {
            Flags = RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength,
            Length = ColumnValueParser.GetLength(length)
        };

        return ColumnValueParser.ReadReference(replay, value, length, column);
    }
}
