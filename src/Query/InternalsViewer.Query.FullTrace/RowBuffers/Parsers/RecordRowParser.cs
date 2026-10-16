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
    private const int LayoutHeaderSize = 4;

    private const int LayoutFixedEndOffset = 2;

    private const int LayoutCountSize = sizeof(ushort);

    private const int LayoutVariableOffsetSize = sizeof(ushort);

    private const byte NullBitmapFlag = 0x10;

    private const byte VariableColumnsFlag = 0x20;

    private const ushort ComplexColumnFlag = 0x8000;

    private const int MaximumRecordSize = 8060;

    private const byte NullStatus = 3;

    private static FixedVarDataRecordLoader Loader { get; } = new(NullLogger<FixedVarDataRecordLoader>.Instance);

    public static byte[]? Read(ITraceReplay replay, ulong address)
    {
        Span<byte> header = stackalloc byte[LayoutHeaderSize];

        if (replay.Read(address, header) != header.Length)
        {
            return null;
        }

        var fixedEnd = BinaryPrimitives.ReadUInt16LittleEndian(header[LayoutFixedEndOffset..]);

        if (fixedEnd < LayoutHeaderSize || fixedEnd > MaximumRecordSize)
        {
            return null;
        }

        if (!replay.TryRead(address + fixedEnd, out ushort columnCount))
        {
            return null;
        }

        var end = fixedEnd + LayoutCountSize + ((header[0] & NullBitmapFlag) != 0 ? GetBitmapSize(columnCount) : 0);

        if ((header[0] & VariableColumnsFlag) != 0)
        {
            if (!replay.TryRead(address + (ulong)end, out ushort variableCount))
            {
                return null;
            }

            end += LayoutCountSize + variableCount * LayoutVariableOffsetSize;

            if (variableCount > 0 && replay.TryRead(address + (ulong)(end - LayoutVariableOffsetSize), out ushort lastEnd))
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
        => record.Length >= LayoutHeaderSize && GetFixedEnd(record) + LayoutCountSize <= record.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(GetFixedEnd(record)))
            : (ushort)0;

    public static int GetFixedEnd(byte[] record) => BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(LayoutFixedEndOffset));

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

    public static bool Fits(byte[] record, RowRecordLayout layout)
        => record.Length >= LayoutHeaderSize
           && GetColumnCount(record) == layout.Columns.Count
           && LayoutHeaderSize + layout.FixedLength == GetFixedEnd(record);

    private static RowBufferColumn ToColumn(int ordinal, bool isVariableLength, FixedVarRecordField field)
        => new(ordinal,
               new RowBufferBinding(isVariableLength ? -1 : field.Offset, 0, isVariableLength ? 0 : field.Length, field.Offset),
               field.IsNull ? NullStatus : (byte)0,
               isVariableLength ? RowBufferColumnFlags.HasLength : RowBufferColumnFlags.None,
               field.Length,
               field.IsNull ? [] : field.Data.ToArray());

    private static int GetBitmapSize(int columnCount) => (columnCount + 7) / 8;
}
