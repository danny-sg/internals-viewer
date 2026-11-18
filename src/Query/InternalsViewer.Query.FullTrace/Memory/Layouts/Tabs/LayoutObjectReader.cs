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
                                    List<ulong>? pointers = null,
                                    IReadOnlyList<EmbeddedObject>? embedded = null,
                                    IReadOnlyDictionary<ulong, string>? pointerNames = null)
    {
        if (tab.Layout == MemoryTabLayout.Column)
        {
            return ReadObject(memory,
                              tab.Header,
                              tab.Type,
                              memory.Layout.GetFields(tab.Type),
                              address,
                              size,
                              0,
                              new PointerSlots(pointers, pointerNames),
                              embedded);
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
                                    List<ulong>? pointers = null,
                                    IReadOnlyList<EmbeddedObject>? embedded = null,
                                    IReadOnlyDictionary<ulong, string>? pointerNames = null)
        => ReadObject(memory, name, name, fields, address, size, MaximumDepth - 1, new PointerSlots(pointers, pointerNames), embedded);

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
                                           PointerSlots pointers,
                                           IReadOnlyList<EmbeddedObject>? embedded)
    {
        var (withEmbedded, slots) = AddEmbedded(memory.Layout, fields, embedded, pointers, address, size);

        var item = new LayoutObject(name, type, address, size, MemoryTabLayout.Column, AddPointers(withEmbedded, slots, address, size));

        item.Rows.Add(ReadValues(memory, item.Fields, address));

        if (depth >= MaximumDepth)
        {
            return item;
        }

        foreach (var field in item.Fields)
        {
            if (field is { Structure: { } structure, Length: > 0 } && field.Offset + field.Length <= size)
            {
                item.Children.Add(ReadObject(memory,
                                             field.Name,
                                             structure,
                                             memory.Layout.GetFields(structure),
                                             address + (ulong)field.Offset,
                                             field.Length,
                                             depth + 1,
                                             slots,
                                             embedded));
            }
        }

        return item;
    }

    private static (List<MemoryField> Fields, PointerSlots Pointers) AddEmbedded(EngineLayout layout,
                                                                                 List<MemoryField> fields,
                                                                                 IReadOnlyList<EmbeddedObject>? embedded,
                                                                                 PointerSlots pointers,
                                                                                 ulong address,
                                                                                 int size)
    {
        if (embedded is not { Count: > 0 })
        {
            return (fields, pointers);
        }

        var objects = embedded.Where(e => e.Address > address && e.Address < address + (ulong)size).OrderBy(e => e.Address).ToList();

        var starts = GetStarts(fields, objects, pointers.Addresses, address, size);

        var added = new List<MemoryField>();

        Dictionary<ulong, string>? names = null;

        foreach (var item in objects)
        {
            var offset = (int)(item.Address - address);

            if (fields.Concat(added).Any(f => f.Structure is not null && f.Offset <= offset && offset < f.Offset + Math.Max(f.Length, 1)))
            {
                continue;
            }

            var known = layout.GetKnownSize(item.Type);

            if (known > 0 && known <= size - offset)
            {
                added.Add(new MemoryField(item.Type, offset, known) { Structure = item.Type });

                continue;
            }

            if (pointers.Addresses?.Contains(item.Address) == true && pointers.Names?.ContainsKey(item.Address) != true)
            {
                names ??= pointers.Names?.ToDictionary() ?? [];

                names[item.Address] = item.Type;

                continue;
            }

            var end = Math.Min(starts.FirstOrDefault(s => s > offset, size), size);

            added.Add(new MemoryField(item.Type, offset, Math.Min(item.Length, end - offset)) { IsBytes = true });
        }

        var slots = names is null ? pointers : pointers with { Names = names };

        return added.Count == 0 ? (fields, slots) : ([.. fields.Concat(added).OrderBy(f => f.Offset)], slots);
    }

    private static List<int> GetStarts(List<MemoryField> fields,
                                       List<EmbeddedObject> objects,
                                       List<ulong>? pointers,
                                       ulong address,
                                       int size)
        =>
        [
            .. fields.Select(f => f.Offset)
                     .Concat(objects.Select(o => (int)(o.Address - address)))
                     .Concat(pointers?.Where(p => p >= address && p < address + (ulong)size).Select(p => (int)(p - address)) ?? [])
                     .Order()
        ];

    private static List<MemoryField> AddPointers(List<MemoryField> fields,
                                                 PointerSlots pointers,
                                                 ulong address,
                                                 int size)
    {
        if (pointers.Addresses is not { Count: > 0 } addresses)
        {
            return fields;
        }

        var added = addresses.Where(p => p >= address && p + PointerSize <= address + (ulong)size)
                             .Select(p => new MemoryField(GetPointerName(pointers, p, (int)(p - address)), (int)(p - address), PointerSize))
                             .Where(p => !fields.Any(f => !f.IsBytes && IsOverlapping(f, p)))
                             .ToList();

        return added.Count == 0 ? fields : [.. fields.Concat(added).OrderBy(f => f.Offset)];
    }

    private static bool IsOverlapping(MemoryField field, MemoryField pointer)
        => pointer.Offset < field.Offset + field.Length && field.Offset < pointer.Offset + PointerSize;

    private static string GetPointerName(PointerSlots pointers, ulong slot, int offset)
        => pointers.Names?.GetValueOrDefault(slot) ?? $"{PointerName}+0x{offset:X}";

    private static ulong?[] ReadValues(ITraceMemoryReader memory, List<MemoryField> fields, ulong address)
    {
        var values = new ulong?[fields.Count];

        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];

            var start = address + (ulong)field.Offset;

            if (field.Structure is not null || field.IsBytes)
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

    private readonly record struct PointerSlots(List<ulong>? Addresses, IReadOnlyDictionary<ulong, string>? Names);
}
