using System.Buffers.Binary;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Iterators;

public static class IteratorLayout
{
    private const int PointerSize = sizeof(ulong);

    private const int MaximumInputs = 64;

    private const string IteratorClass = "CQScanNew";

    private const string RowsetClass = "CQScanRowsetNew";

    private const string HashClass = "CQScanHash";

    private const string PlanObjectClass = "CXte";

    public static bool IsRowset(EngineLayout layout, string? className) => layout.IsA(className, RowsetClass);

    public static bool IsHash(string? className) => className == HashClass;

    public static List<MemoryField> GetFields(MemoryHistoryStore store,
                                              string? className,
                                              IteratorTargetType type,
                                              ReadOnlySpan<byte> data,
                                              IReadOnlyDictionary<ulong, IteratorTarget> targets)
    {
        var fields = new Dictionary<int, MemoryField>();

        foreach (var field in GetClassFields(store.Layout, className, type))
        {
            fields[field.Offset] = field;
        }

        for (var offset = 0; offset + PointerSize <= data.Length; offset += PointerSize)
        {
            if ((!fields.TryGetValue(offset, out var named) || named.IsPointer)
                && targets.TryGetValue(BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]), out var target)
                && target.Operator?.PlanNodeIdentifier is not null)
            {
                fields[offset] = new MemoryField($"{target.TypeName} › {target.Label}", offset, PointerSize);
            }
        }

        var length = data.Length;

        return [.. fields.Values.Where(f => f.Offset + f.Length <= length).OrderBy(f => f.Offset)];
    }

    public static List<ulong> GetInputs(ITraceMemoryReader memory, ulong iterator, IReadOnlyDictionary<ulong, IteratorTarget> targets)
    {
        var found = new List<ulong>();

        if (!memory.TryRead(iterator + (ulong)memory.Layout.Iterator.InputsField.Offset, out ulong inputs) || inputs == 0)
        {
            return found;
        }

        for (var index = 0; index < MaximumInputs; index++)
        {
            if (!memory.TryRead(inputs + (ulong)(index * PointerSize), out ulong input) || !targets.ContainsKey(input))
            {
                break;
            }

            found.Add(input);
        }

        return found;
    }

    private static List<MemoryField> GetClassFields(EngineLayout layout, string? className, IteratorTargetType type)
    {
        var root = type == IteratorTargetType.PlanObject ? PlanObjectClass : IteratorClass;

        var fields = type == IteratorTargetType.ProfilingIterator ? [] : layout.GetFields(className);

        return layout.IsA(className, root) && fields.Count > 0 ? fields : [.. layout.GetFields(root), .. fields];
    }
}
