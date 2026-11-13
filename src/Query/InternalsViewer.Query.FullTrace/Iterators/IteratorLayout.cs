using System.Buffers.Binary;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

namespace InternalsViewer.Query.FullTrace.Iterators;

public static class IteratorLayout
{
    private const int PointerSize = sizeof(ulong);

    private const int MaximumInputs = 64;

    private const string IteratorClass = "CQScanNew";

    private const string RowsetClass = "CQScanRowsetNew";

    private const string RowsetDynamicClass = "CQScanRowsetDynaNew";

    private const string TableScanClass = "CQScanTableScanNew";

    private const string RangeClass = "CQScanRangeNew";

    private const string TopClass = "CQScanTopNew";

    private const string HashClass = "CQScanHash";

    private const string FilterClass = "CQScanFilterNew";

    private const string StreamAggregateClass = "CQScanStreamAggregateNew";

    private const string PlanTopClass = "CXteTop";

    private const string PlanTableScanClass = "CXteTableScan";

    private const string PlanRangeClass = "CXteRange";

    private static readonly Dictionary<string, string> BaseClasses = new()
    {
        [RowsetClass] = IteratorClass,
        [RowsetDynamicClass] = RowsetClass,
        [TableScanClass] = RowsetDynamicClass,
        [RangeClass] = RowsetClass,
        [TraceSortTableReader.IteratorClass] = RowsetClass,
        [TopClass] = IteratorClass,
        [HashClass] = IteratorClass,
        [FilterClass] = IteratorClass,
        [StreamAggregateClass] = IteratorClass
    };

    public static bool IsRowset(string? className) => IsA(className, RowsetClass);

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
            if (!fields.ContainsKey(offset)
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
        if (type == IteratorTargetType.PlanObject)
        {
            List<MemoryField> planFields = className switch
            {
                PlanTopClass => [layout.PlanObject.TopCountField],
                PlanTableScanClass or PlanRangeClass => [layout.PlanObject.RowsetIdField],
                _ => []
            };

            return [.. layout.PlanObject.Fields, .. planFields];
        }

        IReadOnlyList<MemoryField> fields = type == IteratorTargetType.ProfilingIterator
            ? []
            : className switch
            {
                TopClass => [layout.TopIterator.TopCountField],
                HashClass => [.. layout.HashIterator.ValueArrayFields, .. layout.HashIterator.TableFields],
                _ when IsRowset(className) => layout.RowsetIterator.Fields,
                _ => []
            };

        return [layout.Iterator.InputsField, layout.Iterator.ExecutionContextField, .. fields];
    }

    private static bool IsA(string? className, string baseClass)
    {
        for (var current = className; current is not null; current = BaseClasses.GetValueOrDefault(current))
        {
            if (current == baseClass)
            {
                return true;
            }
        }

        return false;
    }
}
