using System.Buffers.Binary;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Iterators;

public static class IteratorLayout
{
    public const int InputsOffset = 0x08;

    public const int ExecutionContextOffset = 0x18;

    public const int BuildOutputValuesOffset = 0x58;

    public const int BuildInputValuesOffset = 0xC8;

    public const int ScanRowsetOffset = 0x48;

    private const int PointerSize = sizeof(ulong);

    private const int MaximumInputs = 64;

    private const int PrefetchManagerOffset = 0x70;

    private const int ClientRowOffset = 0x168;

    private const int TopCountOffset = 0x90;

    private const int ProbeOutputValuesOffset = 0x60;

    private const int ProbeInputValuesOffset = 0xD0;

    private const int PlanInputsOffset = 0x18;

    private const int PlanTopCountOffset = 0x88;

    private const int RowsetIdOffset = 0xB0;

    private const string TableScanClass = "CQScanTableScanNew";

    private const string RangeClass = "CQScanRangeNew";

    private const string TopClass = "CQScanTopNew";

    private const string HashClass = "CQScanHash";

    private const string PlanTopClass = "CXteTop";

    private const string PlanTableScanClass = "CXteTableScan";

    private const string PlanRangeClass = "CXteRange";

    private static readonly List<MemoryField> IteratorFields =
    [
        new("Inputs", InputsOffset, PointerSize),
        new("Execution Context", ExecutionContextOffset, PointerSize)
    ];

    private static readonly List<MemoryField> RowsetFields =
    [
        new("Rowset", ScanRowsetOffset, PointerSize),
        new("Prefetch Manager", PrefetchManagerOffset, PointerSize),
        new("Row Buffer", (int)AccessorBindingLayout.ScanRowBufferOffset, PointerSize),
        new("Row Bindings", (int)AccessorBindingLayout.ScanBindingsOffset, PointerSize),
        new("Client Row", ClientRowOffset, PointerSize)
    ];

    private static readonly List<MemoryField> TopFields = [new("Top Count Expression", TopCountOffset, PointerSize)];

    private static readonly List<MemoryField> PlanObjectFields =
    [
        new("Node Id", (int)PlanNodeLayout.SqlServer2025.PlanNodeIdOffset, sizeof(int)),
        new("Inputs", PlanInputsOffset, PointerSize),
        new("Query", (int)PlanNodeLayout.SqlServer2025.PlanQueryOffset, PointerSize),
        new("Output Columns", (int)PlanNodeLayout.SqlServer2025.PlanOutputsOffset, PointerSize)
    ];

    private static readonly List<MemoryField> PlanTopFields = [new("Top Count Expression", PlanTopCountOffset, PointerSize)];

    private static readonly List<MemoryField> PlanRowsetFields = [new("Rowset Id", RowsetIdOffset, PointerSize)];

    private static readonly List<MemoryField> HashValueFields =
    [
        new("Build Output Values", BuildOutputValuesOffset, PointerSize),
        new("Probe Output Values", ProbeOutputValuesOffset, PointerSize),
        new("Build Input Values", BuildInputValuesOffset, PointerSize),
        new("Probe Input Values", ProbeInputValuesOffset, PointerSize)
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
