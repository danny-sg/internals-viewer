using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.Interfaces;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

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

    public static string? Read(IMemoryReader memory, PlanOutputStage stage)
    {
        if (stage.ColumnType is not { } type
            || !SqlTypeHelpers.TryGetSqlType(type.TypeId, out var sqlType)
            || stage.Value is not { } source
            || source.Follow(memory) is not { } address)
        {
            return null;
        }

        return source.Type == PlanOutputLinkType.BufferEntry
            ? ReadEntry(memory, type, sqlType, address)
            : ReadRowColumn(memory, address, source.Offset ?? 0, type, sqlType);
    }

    public static PlanOutputRecord? ReadRecord(IMemoryReader memory,
                                               TraceBufferPool bufferPool,
                                               TracePosition position,
                                               PlanOutputStage stage)
        => stage.Record is { } source
           && (source.Offset is not null || source.ColumnId > 0)
           && source.Follow(memory) is { } record
           && bufferPool.GetFrameReference(record, position) is { } location
            ? new PlanOutputRecord(location, source.Offset ?? 0, source.ColumnId)
            : null;

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
