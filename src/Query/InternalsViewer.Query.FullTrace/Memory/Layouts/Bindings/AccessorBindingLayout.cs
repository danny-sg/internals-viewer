using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

public static class AccessorBindingLayout
{
    public const string ClassName = "AccBindings";

    public const string ColumnsClassName = "ColumnBinding";

    public const int StatusSize = sizeof(byte);

    public const int LengthSize = sizeof(ulong);

    private const int MaximumColumns = 4096;

    public static (int ColumnCount, ulong Columns)? ReadHeader(ITraceMemoryReader memory, ulong address)
    {
        var layout = memory.Layout.RowBindings;

        return memory.TryRead(address + (ulong)layout.ColumnCountField.Offset, out ushort count)
               && memory.TryRead(address + (ulong)layout.ColumnBindingsField.Offset, out ulong columns)
            ? (Math.Min((int)count, MaximumColumns), columns)
            : null;
    }

    public static List<ColumnBinding> ReadColumns(ITraceMemoryReader memory, ulong address, int count)
    {
        var size = memory.Layout.ColumnBinding.Size;

        var columns = new List<ColumnBinding>(count);

        for (var index = 0; index < count; index++)
        {
            if (ReadColumn(memory, address + (ulong)(index * size), index) is { } column)
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

    private static ColumnBinding? ReadColumn(ITraceMemoryReader memory, ulong address, int index)
    {
        var layout = memory.Layout.ColumnBinding;

        if (!memory.TryRead(address + (ulong)layout.RecordOffsetField.Offset, out int recordOffset)
            || !memory.TryRead(address + (ulong)layout.OrdinalField.Offset, out ushort ordinal)
            || !memory.TryRead(address + (ulong)layout.ValueOffsetField.Offset, out int valueOffset)
            || !memory.TryRead(address + (ulong)layout.StatusOffsetField.Offset, out int statusOffset)
            || !memory.TryRead(address + (ulong)layout.MaximumLengthField.Offset, out ushort maximumLength)
            || !memory.TryRead(address + (ulong)layout.TypeField.Offset, out byte type)
            || !memory.TryRead(address + (ulong)layout.FlagsField.Offset, out uint value))
        {
            return null;
        }

        var flags = (ColumnBindingFlags)value;

        memory.TryRead(address + (ulong)layout.ColumnAttributesField.Offset, out ulong columnAttributes);

        memory.TryRead(address + (ulong)layout.InfrequentDataField.Offset, out ulong infrequentData);

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

    private static int? GetLengthOffset(ITraceMemoryReader memory, ulong infrequentData, ColumnBindingFlags flags)
        => (flags & ColumnBindingFlags.Length) != 0
           && infrequentData != 0
           && memory.TryRead(infrequentData + (ulong)memory.Layout.ColumnBinding.LengthOffsetField.Offset, out int lengthOffset)
            ? lengthOffset
            : null;
}
