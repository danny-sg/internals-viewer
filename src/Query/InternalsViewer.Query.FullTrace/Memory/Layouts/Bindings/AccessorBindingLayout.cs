using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

public static class AccessorBindingLayout
{
    public const string ClassName = "AccBindings";

    public const string ColumnsClassName = "ColumnBinding";

    public const int ColumnSize = 0x50;

    public const int InfrequentDataSize = 0x30;

    public const int StatusSize = sizeof(byte);

    public const int LengthSize = sizeof(ulong);

    public const ulong ColumnsOffset = 0x70;

    private const ulong ColumnCountOffset = 0x62;

    private const ulong MemoryObjectOffset = 0x68;

    private const ulong RecordOffsetOffset = 0x00;

    private const ulong OrdinalOffset = 0x04;

    private const ulong ColumnAttributesOffset = 0x18;

    private const ulong ValueOffsetOffset = 0x28;

    private const ulong StatusOffsetOffset = 0x30;

    private const ulong MaximumLengthOffset = 0x3C;

    private const ulong TypeOffset = 0x3E;

    private const ulong FlagsOffset = 0x40;

    private const ulong InfrequentDataOffset = 0x48;

    private const ulong LengthOffsetOffset = 0x18;

    private const int MaximumColumns = 4096;

    public static List<MemoryField> Fields { get; } =
    [
        new("Column Count", (int)ColumnCountOffset, sizeof(ushort)),
        new("Memory Object", (int)MemoryObjectOffset, sizeof(ulong)),
        new("Column Bindings", (int)ColumnsOffset, sizeof(ulong))
    ];

    public static List<ColumnBindingFieldLayout> ColumnFields { get; } =
    [
        new(ColumnBindingField.RecordOffset, (int)RecordOffsetOffset, sizeof(int)),
        new(ColumnBindingField.Ordinal, (int)OrdinalOffset, sizeof(ushort)),
        new(ColumnBindingField.ColumnAttributes, (int)ColumnAttributesOffset, sizeof(ulong)),
        new(ColumnBindingField.ValueOffset, (int)ValueOffsetOffset, sizeof(int)),
        new(ColumnBindingField.StatusOffset, (int)StatusOffsetOffset, sizeof(int)),
        new(ColumnBindingField.MaximumLength, (int)MaximumLengthOffset, sizeof(ushort)),
        new(ColumnBindingField.Type, (int)TypeOffset, sizeof(byte)),
        new(ColumnBindingField.Flags, (int)FlagsOffset, sizeof(uint)),
        new(ColumnBindingField.InfrequentData, (int)InfrequentDataOffset, sizeof(ulong)),
        new(ColumnBindingField.LengthOffset, (int)LengthOffsetOffset, sizeof(int), IsInfrequent: true)
    ];

    public static int GetColumnCount(int size) => size / (ColumnSize + InfrequentDataSize);

    public static (int ColumnCount, ulong Columns)? ReadHeader(IMemoryReader memory, ulong address)
        => memory.TryRead(address + ColumnCountOffset, out ushort count) && memory.TryRead(address + ColumnsOffset, out ulong columns)
            ? (Math.Min((int)count, MaximumColumns), columns)
            : null;

    public static List<ColumnBinding> ReadColumns(IMemoryReader memory, ulong address, int count)
    {
        var columns = new List<ColumnBinding>(count);

        for (var index = 0; index < count; index++)
        {
            if (ReadColumn(memory, address + (ulong)(index * ColumnSize), index) is { } column)
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    public static ScanBindingReference? ReadScan(ITraceMemoryReader memory, ulong iterator)
    {
        var rowsetIterator = memory.Layout.RowsetIterator;

        if (!memory.TryRead(iterator + (ulong)rowsetIterator.RowBindingsField.Offset, out ulong bindings)
            || bindings == 0
            || ReadHeader(memory, bindings) is not { } header)
        {
            return null;
        }

        memory.TryRead(iterator + (ulong)rowsetIterator.RowBufferField.Offset, out ulong rowBuffer);

        return new ScanBindingReference(iterator, bindings, header.Columns, rowBuffer);
    }

    public static List<ColumnBinding> ReadScanColumns(ITraceMemoryReader memory, ulong iterator)
        => ReadScan(memory, iterator) is { } scan && ReadHeader(memory, scan.Bindings) is { } header
            ? ReadColumns(memory, header.Columns, header.ColumnCount)
            : [];

    private static ColumnBinding? ReadColumn(IMemoryReader memory, ulong address, int index)
    {
        if (!memory.TryRead(address + RecordOffsetOffset, out int recordOffset)
            || !memory.TryRead(address + OrdinalOffset, out ushort ordinal)
            || !memory.TryRead(address + ValueOffsetOffset, out int valueOffset)
            || !memory.TryRead(address + StatusOffsetOffset, out int statusOffset)
            || !memory.TryRead(address + MaximumLengthOffset, out ushort maximumLength)
            || !memory.TryRead(address + TypeOffset, out byte type)
            || !memory.TryRead(address + FlagsOffset, out uint value))
        {
            return null;
        }

        var flags = (ColumnBindingFlags)value;

        memory.TryRead(address + ColumnAttributesOffset, out ulong columnAttributes);

        memory.TryRead(address + InfrequentDataOffset, out ulong infrequentData);

        return new ColumnBinding(index,
                                 ordinal,
                                 recordOffset,
                                 valueOffset,
                                 statusOffset,
                                 GetLengthOffset(memory, infrequentData, flags),
                                 maximumLength,
                                 type,
                                 flags,
                                 columnAttributes,
                                 infrequentData);
    }

    private static int? GetLengthOffset(IMemoryReader memory, ulong infrequentData, ColumnBindingFlags flags)
        => (flags & ColumnBindingFlags.Length) != 0
           && infrequentData != 0
           && memory.TryRead(infrequentData + LengthOffsetOffset, out int lengthOffset)
            ? lengthOffset
            : null;
}
