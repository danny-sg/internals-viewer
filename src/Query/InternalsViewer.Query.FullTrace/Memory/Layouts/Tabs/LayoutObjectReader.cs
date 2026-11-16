using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public static class LayoutObjectReader
{
    private const int MaximumDepth = 4;

    private const int MaximumRows = 1024;

    private const int PointerSize = sizeof(ulong);

    private const string PointerName = "Pointer";

    public static LayoutObject Read(ITraceMemoryReader memory,
                                    MemoryTab tab,
                                    ulong address,
                                    int size,
                                    List<ulong>? pointers = null)
    {
        if (tab.Layout == MemoryTabLayout.Column)
        {
            return ReadObject(memory, tab.Header, tab.Type, memory.Layout.GetFields(tab.Type), address, size, 0, pointers);
        }

        var stride = memory.Layout.GetStructure(tab.Type).Size;

        var trailing = tab.Trailing is { } type ? memory.Layout.GetStructure(type).Size : 0;

        var count = stride + trailing > 0 ? size / (stride + trailing) : 0;

        var array = ReadArray(memory, tab.Header, tab.Type, address, count, size);

        if (tab.Trailing is { } trailingType && trailing > 0 && count > 0)
        {
            var header = MemoryTabTable.Default.Find(memory.Layout, trailingType)?.Header ?? trailingType;

            array.Children.Add(ReadArray(memory, header, trailingType, address + (ulong)(count * stride), count, count * trailing));
        }

        return array;
    }

    public static LayoutObject Read(ITraceMemoryReader memory,
                                    string name,
                                    List<MemoryField> fields,
                                    ulong address,
                                    int size,
                                    List<ulong>? pointers = null)
        => ReadObject(memory, name, name, fields, address, size, MaximumDepth, pointers);

    public static LayoutObject ReadArray(ITraceMemoryReader memory, string name, string type, ulong address, int count)
        => ReadArray(memory, name, type, address, count, count * memory.Layout.GetStructure(type).Size);

    private static LayoutObject ReadArray(ITraceMemoryReader memory, string name, string type, ulong address, int count, int size)
    {
        var fields = memory.Layout.GetFields(type);

        var stride = memory.Layout.GetStructure(type).Size;

        var array = new LayoutObject(name, type, address, size, MemoryTabLayout.Row, fields) { Stride = stride };

        for (var index = 0; index < Math.Min(count, MaximumRows); index++)
        {
            array.Rows.Add(ReadValues(memory, fields, address + (ulong)(index * stride)));
        }

        return array;
    }

    private static LayoutObject ReadObject(ITraceMemoryReader memory,
                                           string name,
                                           string type,
                                           List<MemoryField> fields,
                                           ulong address,
                                           int size,
                                           int depth,
                                           List<ulong>? pointers)
    {
        var item = new LayoutObject(name, type, address, size, MemoryTabLayout.Column, AddPointers(fields, pointers, address, size));

        item.Rows.Add(ReadValues(memory, item.Fields, address));

        if (depth >= MaximumDepth)
        {
            return item;
        }

        foreach (var field in item.Fields)
        {
            if (field.Structure is { } embedded && field.Offset + field.Length <= size)
            {
                item.Children.Add(ReadObject(memory,
                                             field.Name,
                                             embedded,
                                             memory.Layout.GetFields(embedded),
                                             address + (ulong)field.Offset,
                                             field.Length,
                                             depth + 1,
                                             pointers));
            }
        }

        return item;
    }

    private static List<MemoryField> AddPointers(List<MemoryField> fields,
                                                 List<ulong>? pointers,
                                                 ulong address,
                                                 int size)
    {
        if (pointers is not { Count: > 0 })
        {
            return fields;
        }

        var added = pointers.Where(p => p >= address && p + PointerSize <= address + (ulong)size)
                            .Select(p => (int)(p - address))
                            .Select(o => new MemoryField($"{PointerName}+0x{o:X}", o, PointerSize))
                            .Where(p => !fields.Any(f => p.Offset < f.Offset + f.Length && f.Offset < p.Offset + PointerSize))
                            .ToList();

        return added.Count == 0 ? fields : [.. fields.Concat(added).OrderBy(f => f.Offset)];
    }

    private static ulong?[] ReadValues(ITraceMemoryReader memory, List<MemoryField> fields, ulong address)
    {
        var values = new ulong?[fields.Count];

        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];

            var start = address + (ulong)field.Offset;

            if (field.Structure is not null)
            {
                values[index] = start;

                continue;
            }

            bytes.Clear();

            if (field.Length <= sizeof(ulong) && memory.Read(start, bytes[..field.Length]) == field.Length)
            {
                values[index] = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            }
        }

        return values;
    }
}
