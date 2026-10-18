using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Records;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class RecordRowParser
{
    private const int HeaderSize = 4;

    private const int FixedEndOffset = 2;

    private const int CountSize = sizeof(ushort);

    private const int VariableOffsetSize = sizeof(ushort);

    private const byte NullBitmapFlag = 0x10;

    private const byte VariableColumnsFlag = 0x20;

    private const ushort ComplexColumnFlag = 0x8000;

    private const int MaximumRecordSize = 8060;

    private const byte NullStatus = 3;

    private static FixedVarDataRecordLoader Loader { get; } = new(NullLogger<FixedVarDataRecordLoader>.Instance);

    public static byte[]? Read(ITraceReplay replay, ulong address)
    {
        Span<byte> header = stackalloc byte[HeaderSize];

        if (replay.Read(address, header) != header.Length)
        {
            return null;
        }

        var fixedEnd = BinaryPrimitives.ReadUInt16LittleEndian(header[FixedEndOffset..]);

        if (fixedEnd is < HeaderSize or > MaximumRecordSize)
        {
            return null;
        }

        if (!replay.TryRead(address + fixedEnd, out ushort columnCount))
        {
            return null;
        }

        var end = fixedEnd + CountSize + ((header[0] & NullBitmapFlag) != 0 ? GetBitmapSize(columnCount) : 0);

        if ((header[0] & VariableColumnsFlag) != 0)
        {
            if (!replay.TryRead(address + (ulong)end, out ushort variableCount))
            {
                return null;
            }

            end += CountSize + variableCount * VariableOffsetSize;

            if (variableCount > 0 && replay.TryRead(address + (ulong)(end - VariableOffsetSize), out ushort lastEnd))
            {
                end = Math.Max(end, lastEnd & ~ComplexColumnFlag);
            }
        }

        if (end > MaximumRecordSize)
        {
            return null;
        }

        var record = new byte[end];

        return replay.Read(address, record) == record.Length ? record : null;
    }

    public static ushort GetColumnCount(byte[] record)
        => record.Length >= HeaderSize && GetFixedEnd(record) + CountSize <= record.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(GetFixedEnd(record)))
            : (ushort)0;

    public static int GetFixedEnd(byte[] record) => BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(FixedEndOffset));

    public static RowBufferColumn[] Split(byte[] record, RowRecordLayout? layout)
    {
        if (layout is null || !Fits(record, layout))
        {
            return [new RowBufferColumn(0, RowBufferBinding.None, 0, RowBufferColumnFlags.None, record.Length, record)];
        }

        var loaded = Loader.Load(new DataPage { Data = record }, 0, layout.Structure);

        var fields = loaded.Fields.OfType<FixedVarRecordField>().ToArray();

        var columns = new RowBufferColumn[Math.Min(fields.Length, layout.Columns.Count)];

        for (var index = 0; index < columns.Length; index++)
        {
            columns[index] = ToColumn(index, RowRecordLayout.IsVariableLength(layout.Columns[index]), fields[index]);
        }

        return columns;
    }

    public static RowBufferColumn? GetFixedColumn(byte[] record, int index, int offset, int length)
    {
        if (offset < HeaderSize || length < 0 || offset + length > GetFixedEnd(record))
        {
            return null;
        }

        var binding = new RowBufferBinding(offset, 0, length, (ulong)offset);

        return IsNull(record, index)
            ? new RowBufferColumn(index, binding, NullStatus, RowBufferColumnFlags.None, length, [])
            : new RowBufferColumn(index, binding, 0, RowBufferColumnFlags.None, length, record[offset..(offset + length)]);
    }

    public static bool Fits(byte[] record, RowRecordLayout layout)
        => record.Length >= HeaderSize
           && GetColumnCount(record) == layout.Columns.Count
           && HeaderSize + layout.FixedLength == GetFixedEnd(record);

    private static RowBufferColumn ToColumn(int ordinal, bool isVariableLength, FixedVarRecordField field)
        => new(ordinal,
               new RowBufferBinding(isVariableLength ? -1 : field.Offset, 0, isVariableLength ? 0 : field.Length, field.Offset),
               field.IsNull ? NullStatus : (byte)0,
               isVariableLength ? RowBufferColumnFlags.HasLength : RowBufferColumnFlags.None,
               field.Length,
               field.IsNull ? [] : field.Data.ToArray());

    private static int GetBitmapSize(int columnCount) => (columnCount + 7) / 8;

    private static bool IsNull(byte[] record, int index)
    {
        var bitmap = GetFixedEnd(record) + CountSize + index / 8;

        return (record[0] & NullBitmapFlag) != 0
               && index < GetColumnCount(record)
               && bitmap < record.Length
               && (record[bitmap] & (1 << (index % 8))) != 0;
    }
}
