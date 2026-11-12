using System.Buffers.Binary;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Iterators;

public static class IteratorLayout
{
    private const int PointerSize = sizeof(ulong);

    private const int MaximumInputs = 64;

    private const string TableScanClass = "CQScanTableScanNew";

    private const string RangeClass = "CQScanRangeNew";

    private const string TopClass = "CQScanTopNew";

    private const string HashClass = "CQScanHash";

    private const string PlanTopClass = "CXteTop";

    private const string PlanTableScanClass = "CXteTableScan";

    private const string PlanRangeClass = "CXteRange";

    private static readonly List<MemoryField> IteratorFields =
    [
        EngineLayout.Iterator.InputsField,
        EngineLayout.Iterator.ExecutionContextField
    ];

    private static readonly List<MemoryField> RowsetFields =
    [
        EngineLayout.ScanIterator.RowsetField,
        EngineLayout.ScanIterator.PrefetchManagerField,
        EngineLayout.ScanIterator.RowBufferField,
        EngineLayout.ScanIterator.RowBindingsField,
        EngineLayout.ScanIterator.ClientRowField
    ];

    private static readonly List<MemoryField> TopFields = [EngineLayout.TopIterator.TopCountField];

    private static readonly List<MemoryField> PlanObjectFields =
    [
        EngineLayout.PlanObject.NodeIdField,
        EngineLayout.PlanObject.InputsField,
        EngineLayout.PlanObject.QueryField,
        EngineLayout.PlanObject.OutputColumnsField
    ];

    private static readonly List<MemoryField> PlanTopFields = [EngineLayout.PlanObject.TopCountField];

    private static readonly List<MemoryField> PlanRowsetFields = [EngineLayout.PlanObject.RowsetIdField];

    private static readonly List<MemoryField> HashValueFields =
    [
        EngineLayout.HashIterator.BuildOutputValuesField,
        EngineLayout.HashIterator.ProbeOutputValuesField,
        EngineLayout.HashIterator.BuildInputValuesField,
        EngineLayout.HashIterator.ProbeInputValuesField
    ];

    private static readonly List<MemoryField> HashFields = [.. HashValueFields, .. TraceHashTableLayout.IteratorFields];

    public static IReadOnlyList<MemoryField> ValueArrayFields => HashValueFields;

    public static bool HasRowBindings(string? className) => className is TableScanClass or RangeClass;

    public static List<MemoryField> GetFields(string? className,
                                              IteratorTargetType type,
                                              ReadOnlySpan<byte> data,
                                              IReadOnlyDictionary<ulong, IteratorTarget> targets)
    {
        var fields = new Dictionary<int, MemoryField>();

        foreach (var field in GetClassFields(className, type))
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

    public static List<ulong> GetInputs(IMemoryReader memory, ulong inputs, IReadOnlyDictionary<ulong, IteratorTarget> targets)
    {
        var found = new List<ulong>();

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

    private static List<MemoryField> GetClassFields(string? className, IteratorTargetType type)
    {
        if (type == IteratorTargetType.PlanObject)
        {
            List<MemoryField> planFields = className switch
            {
                PlanTopClass => PlanTopFields,
                PlanTableScanClass or PlanRangeClass => PlanRowsetFields,
                _ => []
            };

            return [.. PlanObjectFields, .. planFields];
        }

        List<MemoryField> fields = type == IteratorTargetType.ProfilingIterator
            ? []
            : className switch
            {
                TableScanClass or RangeClass => RowsetFields,
                TopClass => TopFields,
                HashClass => HashFields,
                _ => []
            };

        return [.. IteratorFields, .. fields];
    }
}
