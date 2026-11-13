using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.Layouts;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using FieldTable = System.Collections.Generic.IReadOnlyDictionary<(string Type, string Name),
    InternalsViewer.Query.FullTrace.Memory.Layouts.MemoryField>;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed class EngineLayout(string name, Version minimumVersion, Version maximumVersion, FieldTable fields)
{
    public static EngineLayout Latest => All[^1];

    public string Name { get; } = name;

    public Version MinimumVersion { get; } = minimumVersion;

    public Version MaximumVersion { get; } = maximumVersion;

    public BaseIteratorLayout Iterator { get; } = new(fields);

    public BatchIteratorLayout BatchIterator { get; } = new(fields);

    public BatchPlanObjectLayout BatchPlanObject { get; } = new(fields);

    public RowsetIteratorLayout RowsetIterator { get; } = new(fields);

    public TopIteratorLayout TopIterator { get; } = new(fields);

    public StreamAggregateIteratorLayout StreamAggregateIterator { get; } = new(fields);

    public HashIteratorLayout HashIterator { get; } = new(fields);

    public BatchAdapterLayout BatchAdapter { get; } = new(fields);

    public PlanObjectLayout PlanObject { get; } = new(fields);

    public OutputListLayout OutputList { get; } = new(fields);

    public RowBindingsLayout RowBindings { get; } = new();

    public ColumnReferenceLayout ColumnReference { get; } = new(fields);

    public ExpressionLayout Expression { get; } = new(fields);

    public ExecutionContextLayout ExecutionContext { get; } = new(fields);

    public QueryContextLayout QueryContext { get; } = new(fields);

    public QueryScanLayout QueryScan { get; } = new(fields);

    public QueryScanContextLayout QueryScanContext { get; } = new(fields);

    public StatementLayout Statement { get; } = new(fields);

    public RowContextLayout RowContext { get; } = new(fields);

    public HashValueLayout HashValue { get; } = new(fields);

    public StorageValueLayout StorageValue { get; } = new(fields);

    public SwitchValueLayout SwitchValue { get; } = new(fields);

    public CachedSwitchLayout CachedSwitch { get; } = new(fields);

    public ExpressionValueLayout ExpressionValue { get; } = new(fields);

    public ExecutorLayout Executor { get; } = new(fields);

    public ExecValueLayout ExecValue { get; } = new(fields);

    public RowsetLayout Rowset { get; } = new(fields);

    public RowLayout Row { get; } = new(fields);

    public RowBufferLayout RowBuffer { get; } = new(fields);

    public SortIteratorLayout SortIterator { get; } = new(fields);

    public SortRowsetLayout SortRowset { get; } = new(fields);

    public SortDescriptorLayout SortDescriptor { get; } = new(fields);

    public SortEntryLayout SortEntry { get; } = new(fields);

    internal static IReadOnlyList<EngineLayout> All { get; } = EngineLayoutReader.ReadAll();

    public static EngineLayout Get(Version? version)
    {
        if (version is null)
        {
            return Latest;
        }

        var normalised = new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

        return All.FirstOrDefault(l => l.MinimumVersion <= normalised && normalised <= l.MaximumVersion)
               ?? All.LastOrDefault(l => l.MinimumVersion <= normalised)
               ?? All[0];
    }

    internal static EngineLayout Find(MemoryHistoryStore store, IReadOnlyList<IteratorPlanNode> planNodes)
    {
        var planObjects = new Dictionary<ulong, ulong>();

        foreach (var planNode in planNodes)
        {
            if (!planNode.IsBatchHelper && planNode.BatchPlanObject == 0)
            {
                planObjects.TryAdd(planNode.Iterator, planNode.PlanObject);
            }
        }

        var counts = new int[All.Count];

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var address = store.Blocks[block].Address;

            if (!planObjects.TryGetValue(address, out var planObject) || store.GetWrites(block) is not { Count: > 0 } writes)
            {
                continue;
            }

            var memory = new MemoryHistoryReader(store, writes[^1].Position);

            for (var index = 0; index < All.Count; index++)
            {
                if (memory.TryRead(address + (ulong)All[index].Iterator.PlanObjectField.Offset, out ulong value) && value == planObject)
                {
                    counts[index]++;
                }
            }
        }

        var best = All.Count - 1;

        for (var index = All.Count - 1; index >= 0; index--)
        {
            if (counts[index] > counts[best])
            {
                best = index;
            }
        }

        return All[best];
    }

    private static MemoryField GetField(FieldTable fields, string type, string name)
        => fields.TryGetValue((type, name), out var field)
            ? field
            : throw new InvalidDataException($"The engine layout has no {type} {name} field");

    public sealed class BaseIteratorLayout(FieldTable fields)
    {
        private const string Type = "Iterator";

        public MemoryField InputsField { get; } = GetField(fields, Type, "Inputs");

        public MemoryField PlanObjectField { get; } = GetField(fields, Type, "Plan Object");

        public MemoryField ExecutionContextField { get; } = GetField(fields, Type, "Execution Context");
    }

    public sealed class BatchIteratorLayout(FieldTable fields)
    {
        public MemoryField PlanObjectField { get; } = GetField(fields, "BatchIterator", "Plan Object");
    }

    public sealed class BatchPlanObjectLayout(FieldTable fields)
    {
        public MemoryField PlanObjectField { get; } = GetField(fields, "BatchPlanObject", "Plan Object");
    }

    public sealed class RowsetIteratorLayout(FieldTable fields)
    {
        private const string Type = "RowsetIterator";

        public MemoryField RowsetField { get; } = GetField(fields, Type, "Rowset");

        public MemoryField PrefetchManagerField { get; } = GetField(fields, Type, "Prefetch Manager");

        public MemoryField RowBufferField { get; } = GetField(fields, Type, "Row Buffer");

        public MemoryField RowBindingsField { get; } = GetField(fields, Type, "Row Bindings");

        public MemoryField ClientRowField { get; } = GetField(fields, Type, "Client Row");

        public IReadOnlyList<MemoryField> Fields
            => field ??= [RowsetField, PrefetchManagerField, RowBufferField, RowBindingsField, ClientRowField];
    }

    public sealed class TopIteratorLayout(FieldTable fields)
    {
        public MemoryField TopCountField { get; } = GetField(fields, "TopIterator", "Top Count Expression");
    }

    public sealed class StreamAggregateIteratorLayout(FieldTable fields)
    {
        public const int GroupRowSlot = 1;

        private const string Type = "StreamAggregateIterator";

        public MemoryField CountField { get; } = GetField(fields, Type, "Count");

        public MemoryField VariableTableField { get; } = GetField(fields, Type, "Variable Table");

        public MemoryField VariablesField { get; } = GetField(fields, "VariableTable", "Variables");

        public MemoryField AggregateExecutorField { get; } = GetField(fields, Type, "Aggregate Executor");

        public MemoryField CompareExecutorsField { get; } = GetField(fields, Type, "Compare Executors");

        public MemoryField MoveExecutorsField { get; } = GetField(fields, Type, "Move Executors");
    }

    public sealed class HashIteratorLayout(FieldTable fields)
    {
        private const string Type = "HashIterator";

        public MemoryField BuildOutputValuesField { get; } = GetField(fields, Type, "Build Output Values");

        public MemoryField ProbeOutputValuesField { get; } = GetField(fields, Type, "Probe Output Values");

        public MemoryField BuildInputValuesField { get; } = GetField(fields, Type, "Build Input Values");

        public MemoryField ProbeInputValuesField { get; } = GetField(fields, Type, "Probe Input Values");

        public MemoryField ValueField { get; } = GetField(fields, "HashValueArray", "Value");

        public IReadOnlyList<MemoryField> GroupFields { get; } =
        [
            GetField(fields, Type, "Build Partition Group"),
            GetField(fields, Type, "Build Worktable Group"),
            GetField(fields, Type, "Build Rebuild Group"),
            GetField(fields, Type, "Probe Partition Group"),
            GetField(fields, Type, "Probe Worktable Group"),
            GetField(fields, Type, "Probe Rebuild Group")
        ];

        public MemoryField PartitionMaskField { get; } = GetField(fields, Type, "Partition Mask");

        public MemoryField PartitionShiftField { get; } = GetField(fields, Type, "Partition Shift");

        public MemoryField WorkfileResourceManagerField { get; } = GetField(fields, Type, "Workfile Resource Manager");

        public MemoryField WorkfileManagerField { get; } = GetField(fields, Type, "Workfile Manager");

        public MemoryField CurrentGroupField { get; } = GetField(fields, Type, "Current Partition Group");

        public IReadOnlyList<MemoryField> ValueArrayFields
            => field ??= [BuildOutputValuesField, ProbeOutputValuesField, BuildInputValuesField, ProbeInputValuesField];

        public IReadOnlyList<MemoryField> TableFields
            => field ??=
            [
                .. GroupFields,
                PartitionMaskField,
                PartitionShiftField,
                WorkfileResourceManagerField,
                WorkfileManagerField,
                CurrentGroupField
            ];
    }

    public sealed class BatchAdapterLayout(FieldTable fields)
    {
        private const string Type = "BatchAdapter";

        public MemoryField OutputValuesField { get; } = GetField(fields, Type, "Output Values");

        public MemoryField OutputValueField { get; } = GetField(fields, "BatchOutputValueArray", "Output Value");

        public MemoryField ChildField { get; } = GetField(fields, Type, "Child");

        public MemoryField RowBufferField { get; } = GetField(fields, Type, "Row Buffer");
    }

    public sealed class PlanObjectLayout(FieldTable fields)
    {
        private const string Type = "PlanObject";

        private const string StreamAggregateType = "StreamAggregatePlanObject";

        public MemoryField NodeIdField { get; } = GetField(fields, Type, "Node Id");

        public MemoryField InputsField { get; } = GetField(fields, Type, "Inputs");

        public MemoryField QueryField { get; } = GetField(fields, Type, "Query");

        public MemoryField OutputColumnsField { get; } = GetField(fields, Type, "Output Columns");

        public MemoryField TopCountField { get; } = GetField(fields, "TopPlanObject", "Top Count Expression");

        public MemoryField RowsetIdField { get; } = GetField(fields, "RowsetPlanObject", "Rowset Id");

        public MemoryField GroupCountField { get; } = GetField(fields, StreamAggregateType, "Group Count");

        public MemoryField AggregateCountField { get; } = GetField(fields, StreamAggregateType, "Aggregate Count");

        public MemoryField CountColumnCountField { get; } = GetField(fields, StreamAggregateType, "Count Column Count");

        public MemoryField GroupColumnsField { get; } = GetField(fields, StreamAggregateType, "Group Columns");

        public MemoryField AggregateColumnsField { get; } = GetField(fields, StreamAggregateType, "Aggregate Columns");

        public MemoryField CountColumnsField { get; } = GetField(fields, StreamAggregateType, "Count Columns");

        public IReadOnlyList<MemoryField> Fields => field ??= [NodeIdField, InputsField, QueryField, OutputColumnsField];
    }

    public sealed class OutputListLayout(FieldTable fields)
    {
        private const string Type = "OutputList";

        public MemoryField MemoryObjectField { get; } = GetField(fields, Type, "Memory Object");

        public MemoryField CountField { get; } = GetField(fields, Type, "Count");

        public MemoryField CapacityField { get; } = GetField(fields, Type, "Capacity");

        public MemoryField ColumnsField { get; } = GetField(fields, Type, "Columns");

        public MemoryField ColumnBitCountField { get; } = GetField(fields, Type, "Column Bits Length");

        public MemoryField ColumnBitMemoryObjectField { get; } = GetField(fields, Type, "Column Bits Memory Object");

        public MemoryField ColumnBitsField { get; } = GetField(fields, Type, "Column Bits");

        public MemoryField FlagBitCountField { get; } = GetField(fields, Type, "Column Flags Length");

        public MemoryField FlagBitMemoryObjectField { get; } = GetField(fields, Type, "Column Flags Memory Object");

        public MemoryField FlagBitsField { get; } = GetField(fields, Type, "Column Flags");

        public MemoryField ColumnField { get; } = GetField(fields, "OutputColumnArray", "Column");

        public IReadOnlyList<MemoryField> Fields
            => field ??=
            [
                MemoryObjectField,
                CountField,
                CapacityField,
                ColumnsField,
                ColumnBitCountField,
                ColumnBitMemoryObjectField,
                ColumnBitsField,
                FlagBitCountField,
                FlagBitMemoryObjectField,
                FlagBitsField
            ];
    }

    public sealed class RowBindingsLayout
    {
        public MemoryField ColumnBindingsField { get; } =
            new("Column Bindings", (int)AccessorBindingLayout.ColumnsOffset, sizeof(ulong));

        public MemoryField ColumnBindingField { get; } =
            new("Column Binding", 0x00, AccessorBindingLayout.ColumnSize) { IsNumber = true };
    }

    public sealed class ColumnReferenceLayout(FieldTable fields)
    {
        private const string Type = "ColumnReference";

        private const string StorageType = "StorageReference";

        private const string RedirectType = "RedirectReference";

        public ulong InterfaceOffset { get; } = (ulong)GetField(fields, Type, "Interface").Offset;

        public MemoryField TypeField { get; } = GetField(fields, Type, "Type");

        public MemoryField TypeDetailsField { get; } = GetField(fields, Type, "Type Details");

        public MemoryField ProducerField { get; } = GetField(fields, Type, "Producer");

        public MemoryField StorageProducerField { get; } = GetField(fields, StorageType, "Producer");

        public MemoryField StorageTypeField { get; } = GetField(fields, StorageType, "Type");

        public MemoryField StorageTypeDetailsField { get; } = GetField(fields, StorageType, "Type Details");

        public MemoryField StorageColumnIdField { get; } = GetField(fields, StorageType, "Column Id");

        public MemoryField JoinSideField { get; } = GetField(fields, "JoinReference", "Side");

        public MemoryField JoinColumnField { get; } = GetField(fields, "JoinReference", "Column");

        public MemoryField OrdinalColumnField { get; } = GetField(fields, "OrdinalReference", "Column");

        public MemoryField CalculatedProducerField { get; } = GetField(fields, "CalculatedReference", "Producer");

        public MemoryField ExpressionField { get; } = GetField(fields, "CalculatedReference", "Expression");

        public MemoryField RedirectTargetField { get; } = GetField(fields, RedirectType, "Target");

        public MemoryField RedirectProducerField { get; } = GetField(fields, RedirectType, "Producer");

        public MemoryField RedirectColumnField { get; } = GetField(fields, RedirectType, "Column");
    }

    public sealed class ExpressionLayout(FieldTable fields)
    {
        public MemoryField OperandsField { get; } = GetField(fields, "Expression", "Operands");

        public MemoryField OperandCountField { get; } = GetField(fields, "Expression", "Operand Count");

        public MemoryField OperandField { get; } = GetField(fields, "OperandArray", "Operand");
    }

    public sealed class ExecutionContextLayout(FieldTable fields)
    {
        public MemoryField QueryContextField { get; } = GetField(fields, "ExecutionContext", "Query Execution Context");
    }

    public sealed class QueryContextLayout(FieldTable fields)
    {
        public MemoryField QueryScanField { get; } = GetField(fields, "QueryContext", "Query Scan");
    }

    public sealed class QueryScanLayout(FieldTable fields)
    {
        public MemoryField ContextField { get; } = GetField(fields, "QueryScan", "Query Scan Context");
    }

    public sealed class QueryScanContextLayout(FieldTable fields)
    {
        public MemoryField StatementField { get; } = GetField(fields, "QueryScanContext", "Statement");
    }

    public sealed class StatementLayout(FieldTable fields)
    {
        public MemoryField RowContextField { get; } = GetField(fields, "Statement", "Row Context");

        public MemoryField ExecutorField { get; } = GetField(fields, "Statement", "Output Executor");
    }

    public sealed class RowContextLayout(FieldTable fields)
    {
        public MemoryField SlotField { get; } = GetField(fields, "RowContext", "Slot");
    }

    public sealed class HashValueLayout(FieldTable fields)
    {
        public MemoryField SlotField { get; } = GetField(fields, "HashValue", "Slot");

        public MemoryField RecordOffsetField { get; } = GetField(fields, "HashValue", "Record Offset");
    }

    public sealed class StorageValueLayout(FieldTable fields)
    {
        public MemoryField SlotField { get; } = GetField(fields, "StorageValue", "Slot");

        public MemoryField AccessorField { get; } = GetField(fields, "StorageValue", "Accessor");
    }

    public sealed class SwitchValueLayout(FieldTable fields)
    {
        public MemoryField ReaderField { get; } = GetField(fields, "SwitchValue", "Reader");

        public MemoryField InputField { get; } = GetField(fields, "SwitchValue", "Input");
    }

    public sealed class CachedSwitchLayout(FieldTable fields)
    {
        public int EntryOffset { get; } = GetField(fields, "CachedSwitch", "Entry").Offset;

        public MemoryField ValueField { get; } = GetField(fields, "CachedSwitch", "Value");

        public IReadOnlyList<MemoryField> EntryFields
            => field ??= [.. new RowBufferLayout(fields).EntryFields.Select(GetEntryField)];

        private MemoryField GetEntryField(MemoryField entry)
            => entry with { Name = $"Cached {entry.Name}", Offset = entry.Offset + EntryOffset };
    }

    public sealed class ExpressionValueLayout(FieldTable fields)
    {
        public MemoryField ExecutorField { get; } = GetField(fields, "ExpressionValue", "Executor");
    }

    public sealed class ExecutorLayout(FieldTable fields)
    {
        public MemoryField ValuesField { get; } = GetField(fields, "Executor", "Input Values");

        public MemoryField ValueField { get; } = GetField(fields, "ExecutorValueArray", "Value");

        public MemoryField ElementField { get; } = GetField(fields, "ExecutorArray", "Executor");
    }

    public sealed class ExecValueLayout(FieldTable fields)
    {
        public MemoryField EntryField { get; } = GetField(fields, "ExecValue", "Entry");
    }

    public sealed class RowsetLayout(FieldTable fields)
    {
        public ulong InterfaceOffset { get; } = (ulong)GetField(fields, "Rowset", "Interface").Offset;

        public MemoryField RowField { get; } = GetField(fields, "Rowset", "Row");
    }

    public sealed class RowLayout(FieldTable fields)
    {
        public MemoryField RecordField { get; } = GetField(fields, "Row", "Record");
    }

    public sealed class RowBufferLayout(FieldTable fields)
    {
        private const string Type = "RowBuffer";

        public MemoryField EntryField { get; } = GetField(fields, Type, "Entry");

        public MemoryField StatusField { get; } = GetField(fields, Type, "Status");

        public MemoryField TypeField { get; } = GetField(fields, Type, "Type");

        public MemoryField ValueField { get; } = GetField(fields, Type, "Value");

        public MemoryField LengthField { get; } = GetField(fields, Type, "Length");

        public IReadOnlyList<MemoryField> EntryFields => field ??= [StatusField, TypeField, ValueField, LengthField];
    }

    public sealed class SortIteratorLayout(FieldTable fields)
    {
        public MemoryField PushRowExecutorField { get; } = GetField(fields, "SortIterator", "Push Row Executor");

        public MemoryField MemoryUsageField { get; } = GetField(fields, "SortIterator", "Memory Usage");

        public IReadOnlyList<MemoryField> Fields
            => field ??= GetFields(new BaseIteratorLayout(fields), new RowsetIteratorLayout(fields));

        private List<MemoryField> GetFields(BaseIteratorLayout iterator, RowsetIteratorLayout rowset)
            =>
            [
                iterator.InputsField,
                iterator.PlanObjectField,
                iterator.ExecutionContextField,
                rowset.RowsetField,
                rowset.RowBufferField,
                rowset.RowBindingsField,
                PushRowExecutorField,
                MemoryUsageField
            ];
    }

    public sealed class SortRowsetLayout(FieldTable fields)
    {
        public MemoryField DescriptorField { get; } = GetField(fields, "SortRowset", "Sort Descriptor");

        public MemoryField RowCountField { get; } = GetField(fields, "SortRowset", "Row Count");

        public MemoryField KeyColumnsField { get; } = GetField(fields, "SortRowset", "Key Columns");

        public IReadOnlyList<MemoryField> Fields => field ??= [DescriptorField, RowCountField, KeyColumnsField];
    }

    public sealed class SortDescriptorLayout(FieldTable fields)
    {
        public const int RunCount = 32;

        private const string Type = "SortDescriptor";

        public MemoryField ParametersField { get; } = GetField(fields, Type, "Sort Parameters");

        public MemoryField KeyColumnsField { get; } = GetField(fields, Type, "Key Columns");

        public MemoryField MemoryUsageField { get; } = GetField(fields, Type, "Memory Usage");

        public MemoryField EstimatedRowsField { get; } = GetField(fields, Type, "Estimated Rows");

        public MemoryField RecordLengthField { get; } = GetField(fields, Type, "Fixed Record Length");

        public MemoryField StateField { get; } = GetField(fields, Type, "State");

        public IReadOnlyList<MemoryField> RunFields { get; } = GetRuns(GetField(fields, Type, "Run"));

        public MemoryField CursorField { get; } = GetField(fields, Type, "Cursor");

        public MemoryField CurrentListField { get; } = GetField(fields, Type, "Current List");

        public MemoryField CurrentListTailField { get; } = GetField(fields, Type, "Current List Tail");

        public MemoryField CurrentListCountField { get; } = GetField(fields, Type, "Current List Count");

        public MemoryField EntrySizeField { get; } = GetField(fields, Type, "Entry Size");

        public MemoryField LinkedCountField { get; } = GetField(fields, Type, "Linked Entries");

        public IReadOnlyList<MemoryField> RunHeadFields => field ??= [.. RunFields, CursorField, CurrentListField];

        public IReadOnlyList<MemoryField> Fields
            => field ??=
            [
                ParametersField,
                KeyColumnsField,
                MemoryUsageField,
                EstimatedRowsField,
                RecordLengthField,
                StateField,
                .. RunFields,
                CursorField,
                CurrentListField,
                CurrentListTailField,
                CurrentListCountField,
                EntrySizeField,
                LinkedCountField
            ];

        private static List<MemoryField> GetRuns(MemoryField run)
            => [.. Enumerable.Range(0, RunCount).Select(i => run with { Name = $"{run.Name} {i}", Offset = run.Offset + i * run.Length })];
    }

    public sealed class SortEntryLayout(FieldTable fields)
    {
        public const int Size = 0x28;

        private const string Type = "SortEntry";

        public MemoryField RecordField { get; } = GetField(fields, Type, "Record");

        public MemoryField KeyField { get; } = GetField(fields, Type, "Key");

        public MemoryField RecordLengthField { get; } = GetField(fields, Type, "Record Length");

        public MemoryField KeyLengthField { get; } = GetField(fields, Type, "Key Length");

        public MemoryField NextField { get; } = GetField(fields, Type, "Next");

        public IReadOnlyList<MemoryField> Fields => field ??= [RecordField, KeyField, RecordLengthField, KeyLengthField, NextField];
    }
}
