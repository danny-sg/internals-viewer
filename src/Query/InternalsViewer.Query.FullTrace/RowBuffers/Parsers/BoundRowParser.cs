using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class BoundRowParser
{
    private const ulong MaximumRowBufferBytes = 0x10000;

    public static RowBufferColumn[] Parse(ITraceReplay replay, BindingParser.BindingSet set, ulong buffer)
    {
        var columns = new RowBufferColumn[set.Columns.Length];

        for (var ordinal = 0; ordinal < columns.Length; ordinal++)
        {
            columns[ordinal] = ParseColumn(replay, ordinal, set.Columns[ordinal], buffer);
        }

        return columns;
    }

    private static bool IsInBuffer(ulong offset, ulong size) => offset < MaximumRowBufferBytes && size <= MaximumRowBufferBytes - offset;

    private static RowBufferColumn ParseColumn(ITraceReplay replay, int ordinal, BindingParser.ColumnBinding binding, ulong buffer)
    {
        var column = ColumnValueParser.Create(ordinal, binding.Binding);

        if (binding.Unreadable != RowBufferColumnFlags.None)
        {
            return column with { Flags = RowBufferColumnFlags.Unreadable | binding.Unreadable };
        }

        if (!IsInBuffer(binding.StatusOffset, sizeof(byte))
            || !IsInBuffer(binding.ValueOffset, sizeof(ulong))
            || !replay.TryRead(buffer + binding.StatusOffset, out byte status))
        {
            return column with { Flags = ColumnValueParser.SlotUnreadable };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        var length = (ulong)binding.Binding.FixedLength;

        var flags = RowBufferColumnFlags.None;

        var isLob = (binding.Binding.Flags & BindingParser.LobBinding) != 0;

        if ((binding.Binding.Flags & BindingParser.LengthBinding) != 0)
        {
            if (!IsInBuffer(binding.LengthOffset, sizeof(ulong)) || !replay.TryRead(buffer + binding.LengthOffset, out length))
            {
                return column with { Flags = ColumnValueParser.SlotUnreadable };
            }

            flags |= RowBufferColumnFlags.HasLength;

            if (isLob && length >= BlobHandleParser.QueryHandleSize)
            {
                length -= BlobHandleParser.QueryHandleSize;
            }
        }

        column = column with { Length = ColumnValueParser.GetLength(length), Flags = flags };

        if ((binding.Binding.Flags & BindingParser.ByReferenceBinding) == 0)
        {
            var stored = binding.Binding.FixedLength > 0 ? Math.Min(length, (ulong)binding.Binding.FixedLength) : length;

            column = ColumnValueParser.ReadBytes(replay, buffer + binding.ValueOffset, stored, column);

            return isLob && BlobHandleParser.Parse(column.Data) is { } lob ? ColumnValueParser.Defer(column, lob) : column;
        }

        column = column with { Flags = flags | RowBufferColumnFlags.ByReference };

        if (!replay.TryRead(buffer + binding.ValueOffset, out ulong pointer))
        {
            return column with { Flags = column.Flags | ColumnValueParser.SlotUnreadable };
        }

        return ColumnValueParser.ReadReference(replay, pointer, length, column);
    }
}
