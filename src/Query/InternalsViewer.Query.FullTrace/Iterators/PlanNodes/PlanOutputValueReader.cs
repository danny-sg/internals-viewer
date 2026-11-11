using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

public static class PlanOutputValueReader
{
    private const byte NullStatus = 3;

    private const ulong EntryDataOffset = 0x08;

    private const ulong EntryLengthOffset = 0x10;

    private const int InlineSize = sizeof(ulong);

    private const int MaximumLength = 8000;

    private const string NullText = "NULL";

    private const ulong RowFixedEndOffset = 0x02;

    private const int CountSize = sizeof(ushort);

    private const int BitsPerByte = 8;

    private const ushort ComplexColumnFlag = 0x8000;

    public static string? Read(IMemoryReader memory, PlanOutputColumn column, IReadOnlyList<PlanOutputLink> links)
    {
        if (!SqlTypeHelpers.TryGetSqlType(column.Type.TypeId, out var sqlType))
        {
            return null;
        }

        foreach (var link in links)
        {
            if (link is { Type: PlanOutputLinkType.BufferEntry, Depth: 0 })
            {
                return ReadEntry(memory, column.Type, sqlType, link.Address);
            }
        }

        if (links.FirstOrDefault(l => l is { Type: PlanOutputLinkType.Value or PlanOutputLinkType.Reader, Depth: 0, Offset: not null })
                is not { Offset: { } offset } reader
            || links.FirstOrDefault(l => l is { Type: PlanOutputLinkType.RowSlot, Depth: 0 } && l.Slot == reader.Slot) is not
               { Type: PlanOutputLinkType.RowSlot } slot
            || !memory.TryRead(slot.Address, out ulong row)
            || row < TraceAddress.MinimumAddress)
        {
            return null;
        }

        return ReadRowColumn(memory, row, offset, column.Type, sqlType);
    }

    public static string? ReadEntry(IMemoryReader memory, SqlTypeInfo type, ulong entry)
        => SqlTypeHelpers.TryGetSqlType(type.TypeId, out var sqlType) ? ReadEntry(memory, type, sqlType, entry) : null;

    public static PlanOutputRecord? ReadRecord(IMemoryReader memory,
                                               TraceBufferPool bufferPool,
                                               TracePosition position,
                                               IReadOnlyList<PlanOutputLink> links)
    {
        foreach (var link in links)
        {
            if (link is { Type: PlanOutputLinkType.Record, Depth: 0, Offset: { } columnOffset }
                && memory.TryRead(link.Address, out ulong record)
                && bufferPool.GetFrameReference(record, position) is { } location)
            {
                return new PlanOutputRecord(location, columnOffset);
            }
        }

        return null;
    }

    private static string? ReadEntry(IMemoryReader memory, SqlTypeInfo type, SqlDbType sqlType, ulong entry)
    {
        if (!memory.TryRead(entry, out byte status))
        {
            return null;
        }

        if (status == NullStatus)
        {
            return NullText;
        }

        if (IsFixed(type) && type.MaxLength <= InlineSize)
        {
            return Decode(memory, entry + EntryDataOffset, type.MaxLength, type, sqlType);
        }

        if (!memory.TryRead(entry + EntryDataOffset, out ulong data) || !memory.TryRead(entry + EntryLengthOffset, out ulong length))
        {
            return null;
        }

        return length == 0 && !IsFixed(type)
            ? string.Empty
            : Decode(memory, data, (int)Math.Min(length == 0 ? type.MaxLength : length, MaximumLength), type, sqlType);
    }

    private static string? ReadRowColumn(IMemoryReader memory, ulong row, int offset, SqlTypeInfo type, SqlDbType sqlType)
    {
        if (offset >= 0)
        {
            return IsVariable(type) ? null : Decode(memory, row + (ulong)offset, type.MaxLength, type, sqlType);
        }

        if (!memory.TryRead(row + RowFixedEndOffset, out ushort fixedEnd)
            || !memory.TryRead(row + fixedEnd, out ushort columnCount))
        {
            return null;
        }

        var variableCountOffset = fixedEnd + CountSize + (columnCount + BitsPerByte - 1) / BitsPerByte;

        var index = -offset - 1;

        if (!memory.TryRead(row + (ulong)variableCountOffset, out ushort variableCount) || index >= variableCount)
        {
            return null;
        }

        var endsOffset = variableCountOffset + CountSize;

        var start = index == 0 ? endsOffset + variableCount * CountSize : ReadEnd(memory, row, endsOffset, index - 1);

        var end = ReadEnd(memory, row, endsOffset, index);

        if (start < 0 || end < start)
        {
            return null;
        }

        return end == start ? string.Empty : Decode(memory, row + (ulong)start, end - start, type, sqlType);
    }

    private static int ReadEnd(IMemoryReader memory, ulong row, int endsOffset, int index)
        => memory.TryRead(row + (ulong)(endsOffset + index * CountSize), out ushort end) ? end & ~ComplexColumnFlag : -1;

    private static bool IsVariable(SqlTypeInfo type) => type.TypeId is 34 or 35 or 98 or 99 or 165 or 167 or 231 or 240 or 241 or 244;

    private static string? Decode(IMemoryReader memory, ulong address, int length, SqlTypeInfo type, SqlDbType sqlType)
    {
        if (length <= 0)
        {
            return null;
        }

        var data = new byte[length];

        return memory.Read(address, data) == length ? DataConverter.BinaryToString(data, sqlType, type.Precision, type.Scale) : null;
    }

    private static bool IsFixed(SqlTypeInfo type) => type.TypeId is not (34 or 35 or 98 or 99 or 165 or 167 or 173 or 175
                                                                         or 231 or 239 or 240 or 241 or 244);
}
