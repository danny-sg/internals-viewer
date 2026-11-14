using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using StructureTable = System.Collections.Generic.IReadOnlyDictionary<string,
    InternalsViewer.Query.FullTrace.Memory.Layouts.MemoryStructure>;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public sealed class EngineLayout(string name, Version minimumVersion, Version maximumVersion, StructureTable structures)
{
    public static EngineLayout Latest => All[^1];

    public string Name { get; } = name;

    public Version MinimumVersion { get; } = minimumVersion;

    public Version MaximumVersion { get; } = maximumVersion;

    public BaseIteratorLayout Iterator { get; } = new(Get(structures, "CQScanNew"));

    public BatchIteratorLayout BatchIterator { get; } = new(Get(structures, "CBpQScan"));

    public BatchPlanObjectLayout BatchPlanObject { get; } = new(Get(structures, "CBpXte"));

    public RowsetIteratorLayout RowsetIterator { get; } = new(Get(structures, "CQScanRowsetNew"));

    public TopIteratorLayout TopIterator { get; } = new(Get(structures, "CQScanTopNew"));

    public StreamAggregateIteratorLayout StreamAggregateIterator { get; } =
        new(Get(structures, "CQScanStreamAggregateNew"), Get(structures, "VariableTable"));

    public HashIteratorLayout HashIterator { get; } = new(Get(structures, "CQScanHash"), Get(structures, "HashValueArray"));

    public BatchAdapterLayout BatchAdapter { get; } = new(Get(structures, "CQScanBatchHelper"), Get(structures, "BatchOutputValueArray"));

    public PlanObjectLayout PlanObject { get; } = new(Get(structures, "CXte"),
                                                      Get(structures, "CXteTop"),
                                                      Get(structures, "CXteTableScan"),
                                                      Get(structures, "CXteStreamAggregate"));

    public OutputListLayout OutputList { get; } = new(Get(structures, "CBndOutputs"), Get(structures, "OutputColumnArray"));

    public RowBindingsLayout RowBindings { get; } = new(Get(structures, "AccBindings"), Get(structures, "ColumnBinding"));

    public ColumnBindingLayout ColumnBinding { get; } = new(Get(structures, "ColumnBinding"), Get(structures, "ColumnBindingInfrequent"));

    public ColumnReferenceLayout ColumnReference { get; } = new(Get(structures, "CValRefComp"),
                                                                Get(structures, "CValRefSS"),
                                                                Get(structures, "CValRefJoin"),
                                                                Get(structures, "CValRefOrdinal"),
                                                                Get(structures, "CValRefCalculated"),
                                                                Get(structures, "CValRefBatchRedirect"));

    public TypeInfoLayout TypeInfo { get; } = new(Get(structures, "CTypeInfo"));

    public ExpressionLayout Expression { get; } = new(Get(structures, "Expression"), Get(structures, "OperandArray"));

    public ExecutionContextLayout ExecutionContext { get; } = new(Get(structures, "CExecContext"));

    public QueryContextLayout QueryContext { get; } = new(Get(structures, "CQueryExecContext"));

    public QueryScanLayout QueryScan { get; } = new(Get(structures, "CQueryScan"));

    public QueryScanContextLayout QueryScanContext { get; } = new(Get(structures, "CQECtxt"));

    public StatementLayout Statement { get; } = new(Get(structures, "CXStmtQuery"));

    public RowContextLayout RowContext { get; } = new(Get(structures, "RowContext"));

    public HashValueLayout HashValue { get; } = new(Get(structures, "CValHash"));

    public StorageValueLayout StorageValue { get; } = new(Get(structures, "CValSSTable"));

    public SwitchValueLayout SwitchValue { get; } = new(Get(structures, "CValHashSwitch"));

    public CachedSwitchLayout CachedSwitch { get; } = new(Get(structures, "CValHashCachedSwitch"));

    public ExpressionValueLayout ExpressionValue { get; } = new(Get(structures, "CValExpr"));

    public ExecutorLayout Executor { get; } = new(Get(structures, "CEsExec"),
                                                  Get(structures, "ExecutorValueArray"),
                                                  Get(structures, "ExecutorArray"));

    public ExecValueLayout ExecValue { get; } = new(Get(structures, "CValExec"));

    public RowsetLayout Rowset { get; } = new(Get(structures, "RowsetNewSS"));

    public RowLayout Row { get; } = new(Get(structures, "Row"));

    public RowBufferLayout RowBuffer { get; } = new(Get(structures, "XVariant"));

    public SortIteratorLayout SortIterator { get; } = new(Get(structures, "CQScanSortNew"));

    public SortRowsetLayout SortRowset { get; } = new(Get(structures, "RowsetSorted"));

    public SortDescriptorLayout SortDescriptor { get; } = new(Get(structures, "SORTDES"));

    public SortEntryLayout SortEntry { get; } = new(Get(structures, "SortEntry"));

    public HashGroupLayout HashGroup { get; } = new(Get(structures, "CHashWorkfilePartitionGroup"));

    public HashPartitionLayout HashPartition { get; } = new(Get(structures, "CHashWorkfilePartitionInstance"));

    public HashItemLayout HashItem { get; } = new(Get(structures, "HashItem"));

    public BufferLayout Buffer { get; } = new(Get(structures, "BUF"));

    public BatchLayout Batch { get; } = new(Get(structures, "CBpBatch"), Get(structures, "BatchVector"));

    public DeepDataPageLayout DeepDataPage { get; } = new(Get(structures, "DeepDataPage"));

    public PageLocationLayout PageLocation { get; } = new(Get(structures, "AllocationPageId"), Get(structures, "PageHandle"));

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

    public MemoryStructure GetStructure(string type) => Get(structures, type);

    public bool IsA(string? className, string baseClass) => GetClassChain(className).Any(s => s.Name == baseClass);

    public List<MemoryField> GetFields(string? className)
    {
        var fields = new Dictionary<int, MemoryField>();

        foreach (var structure in GetClassChain(className).Reverse())
        {
            foreach (var field in structure.Fields)
            {
                fields[field.Offset] = field;
            }
        }

        return [.. fields.Values.OrderBy(f => f.Offset)];
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

    private static MemoryStructure Get(StructureTable structures, string type)
        => structures.TryGetValue(type, out var structure)
            ? structure
            : throw new InvalidDataException($"The engine layout has no {type} structure");

    private IEnumerable<MemoryStructure> GetClassChain(string? className)
    {
        for (var current = className; current is not null && structures.TryGetValue(current, out var structure); current = structure.Base)
        {
            yield return structure;
        }
    }

    public sealed class BaseIteratorLayout(MemoryStructure structure)
    {
        public MemoryField InputsField { get; } = structure.Get("Inputs");

        public MemoryField PlanObjectField { get; } = structure.Get("Plan Object");

        public MemoryField ExecutionContextField { get; } = structure.Get("Execution Context");
    }

    public sealed class BatchIteratorLayout(MemoryStructure structure)
    {
        public MemoryField PlanObjectField { get; } = structure.Get("Plan Object");
    }

    public sealed class BatchPlanObjectLayout(MemoryStructure structure)
    {
        public MemoryField PlanObjectField { get; } = structure.Get("Plan Object");
    }

    public sealed class RowsetIteratorLayout(MemoryStructure structure)
    {
        public MemoryField RowsetField { get; } = structure.Get("Rowset");

        public MemoryField PrefetchManagerField { get; } = structure.Get("Prefetch Manager");

        public MemoryField RowBufferField { get; } = structure.Get("Row Buffer");

        public MemoryField RowBindingsField { get; } = structure.Get("Row Bindings");

        public MemoryField ClientRowField { get; } = structure.Get("Client Row");
    }

    public sealed class TopIteratorLayout(MemoryStructure structure)
    {
        public MemoryField TopCountField { get; } = structure.Get("Top Count Expression");
    }

    public sealed class StreamAggregateIteratorLayout(MemoryStructure structure, MemoryStructure variables)
    {
        public const int GroupRowSlot = 1;

        public MemoryField CountField { get; } = structure.Get("Count");

        public MemoryField VariableTableField { get; } = structure.Get("Variable Table");

        public MemoryField VariablesField { get; } = variables.Get("Variables");

        public MemoryField AggregateExecutorField { get; } = structure.Get("Aggregate Executor");

        public MemoryField CompareExecutorsField { get; } = structure.Get("Compare Executors");

        public MemoryField MoveExecutorsField { get; } = structure.Get("Move Executors");
    }

    public sealed class HashIteratorLayout(MemoryStructure structure, MemoryStructure values)
    {
        public MemoryField BuildOutputValuesField { get; } = structure.Get("Build Output Values");

        public MemoryField ProbeOutputValuesField { get; } = structure.Get("Probe Output Values");

        public MemoryField BuildInputValuesField { get; } = structure.Get("Build Input Values");

        public MemoryField ProbeInputValuesField { get; } = structure.Get("Probe Input Values");

        public MemoryField ValueField { get; } = values.Get("Value");

        public IReadOnlyList<MemoryField> GroupFields { get; } =
        [
            structure.Get("Build Partition Group"),
            structure.Get("Build Worktable Group"),
            structure.Get("Build Rebuild Group"),
            structure.Get("Probe Partition Group"),
            structure.Get("Probe Worktable Group"),
            structure.Get("Probe Rebuild Group")
        ];

        public MemoryField PartitionMaskField { get; } = structure.Get("Partition Mask");

        public MemoryField PartitionShiftField { get; } = structure.Get("Partition Shift");

        public MemoryField WorkfileResourceManagerField { get; } = structure.Get("Workfile Resource Manager");

        public MemoryField WorkfileManagerField { get; } = structure.Get("Workfile Manager");

        public MemoryField CurrentGroupField { get; } = structure.Get("Current Partition Group");

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

    public sealed class BatchAdapterLayout(MemoryStructure structure, MemoryStructure values)
    {
        public MemoryField OutputValuesField { get; } = structure.Get("Output Values");

        public MemoryField OutputValueField { get; } = values.Get("Output Value");

        public MemoryField ChildField { get; } = structure.Get("Child");

        public MemoryField RowBufferField { get; } = structure.Get("Row Buffer");
    }

    public sealed class PlanObjectLayout(MemoryStructure structure,
                                         MemoryStructure top,
                                         MemoryStructure tableScan,
                                         MemoryStructure streamAggregate)
    {
        public MemoryField NodeIdField { get; } = structure.Get("Node Id");

        public MemoryField InputsField { get; } = structure.Get("Inputs");

        public MemoryField QueryField { get; } = structure.Get("Query");

        public MemoryField OutputColumnsField { get; } = structure.Get("Output Columns");

        public MemoryField TopCountField { get; } = top.Get("Top Count Expression");

        public MemoryField RowsetIdField { get; } = tableScan.Get("Rowset Id");

        public MemoryField GroupCountField { get; } = streamAggregate.Get("Group Count");

        public MemoryField AggregateCountField { get; } = streamAggregate.Get("Aggregate Count");

        public MemoryField CountColumnCountField { get; } = streamAggregate.Get("Count Column Count");

        public MemoryField GroupColumnsField { get; } = streamAggregate.Get("Group Columns");

        public MemoryField AggregateColumnsField { get; } = streamAggregate.Get("Aggregate Columns");

        public MemoryField CountColumnsField { get; } = streamAggregate.Get("Count Columns");
    }

    public sealed class OutputListLayout(MemoryStructure structure, MemoryStructure columns)
    {
        public MemoryField MemoryObjectField { get; } = structure.Get("Memory Object");

        public MemoryField CountField { get; } = structure.Get("Count");

        public MemoryField CapacityField { get; } = structure.Get("Capacity");

        public MemoryField ColumnsField { get; } = structure.Get("Columns");

        public MemoryField ColumnBitCountField { get; } = structure.Get("Column Bits Length");

        public MemoryField ColumnBitMemoryObjectField { get; } = structure.Get("Column Bits Memory Object");

        public MemoryField ColumnBitsField { get; } = structure.Get("Column Bits");

        public MemoryField FlagBitCountField { get; } = structure.Get("Column Flags Length");

        public MemoryField FlagBitMemoryObjectField { get; } = structure.Get("Column Flags Memory Object");

        public MemoryField FlagBitsField { get; } = structure.Get("Column Flags");

        public MemoryField ColumnField { get; } = columns.Get("Column");
    }

    public sealed class RowBindingsLayout(MemoryStructure structure, MemoryStructure column)
    {
        public MemoryField ColumnCountField { get; } = structure.Get("Column Count");

        public MemoryField MemoryObjectField { get; } = structure.Get("Memory Object");

        public MemoryField ColumnBindingsField { get; } = structure.Get("Column Bindings");

        public MemoryField ColumnBindingField { get; } = new("Column Binding", 0x00, column.Size) { IsNumber = true };

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class ColumnBindingLayout(MemoryStructure structure, MemoryStructure infrequent)
    {
        public int Size { get; } = structure.Size;

        public int InfrequentSize { get; } = infrequent.Size;

        public MemoryField RecordOffsetField { get; } = structure.Get("Record Offset");

        public MemoryField OrdinalField { get; } = structure.Get("Ordinal");

        public MemoryField ColumnAttributesField { get; } = structure.Get("Column Attributes");

        public MemoryField ValueOffsetField { get; } = structure.Get("Value Offset");

        public MemoryField StatusOffsetField { get; } = structure.Get("Status Offset");

        public MemoryField MaximumLengthField { get; } = structure.Get("Maximum Length");

        public MemoryField TypeField { get; } = structure.Get("Type");

        public MemoryField FlagsField { get; } = structure.Get("Flags");

        public MemoryField InfrequentDataField { get; } = structure.Get("Infrequent Data");

        public MemoryField LengthOffsetField { get; } = infrequent.Get("Length Offset");

        public IReadOnlyList<ColumnBindingFieldLayout> Fields
            => field ??=
            [
                Create(ColumnBindingField.RecordOffset, RecordOffsetField),
                Create(ColumnBindingField.Ordinal, OrdinalField),
                Create(ColumnBindingField.ColumnAttributes, ColumnAttributesField),
                Create(ColumnBindingField.ValueOffset, ValueOffsetField),
                Create(ColumnBindingField.StatusOffset, StatusOffsetField),
                Create(ColumnBindingField.MaximumLength, MaximumLengthField),
                Create(ColumnBindingField.Type, TypeField),
                Create(ColumnBindingField.Flags, FlagsField),
                Create(ColumnBindingField.InfrequentData, InfrequentDataField),
                Create(ColumnBindingField.LengthOffset, LengthOffsetField, true)
            ];

        public int GetColumnCount(int size) => size / (Size + InfrequentSize);

        private static ColumnBindingFieldLayout Create(ColumnBindingField kind, MemoryField field, bool isInfrequent = false)
            => new(kind, field.Offset, field.Length, isInfrequent);
    }

    public sealed class ColumnReferenceLayout(MemoryStructure structure,
                                              MemoryStructure storage,
                                              MemoryStructure join,
                                              MemoryStructure ordinal,
                                              MemoryStructure calculated,
                                              MemoryStructure redirect)
    {
        public ulong InterfaceOffset { get; } = (ulong)structure.Get("Interface").Offset;

        public MemoryField TypeField { get; } = structure.Get("Type");

        public MemoryField TypeDetailsField { get; } = structure.Get("Type Details");

        public MemoryField ProducerField { get; } = structure.Get("Producer");

        public MemoryField StorageProducerField { get; } = storage.Get("Producer");

        public MemoryField StorageTypeField { get; } = storage.Get("Type");

        public MemoryField StorageTypeDetailsField { get; } = storage.Get("Type Details");

        public MemoryField StorageColumnIdField { get; } = storage.Get("Column Id");

        public MemoryField JoinSideField { get; } = join.Get("Side");

        public MemoryField JoinColumnField { get; } = join.Get("Column");

        public MemoryField OrdinalColumnField { get; } = ordinal.Get("Column");

        public MemoryField CalculatedProducerField { get; } = calculated.Get("Producer");

        public MemoryField ExpressionField { get; } = calculated.Get("Expression");

        public MemoryField RedirectTargetField { get; } = redirect.Get("Target");

        public MemoryField RedirectProducerField { get; } = redirect.Get("Producer");

        public MemoryField RedirectColumnField { get; } = redirect.Get("Column");
    }

    public sealed class TypeInfoLayout(MemoryStructure structure)
    {
        public MemoryField TypeField { get; } = structure.Get("Type");

        public MemoryField TypeDetailsField { get; } = structure.Get("Type Details");
    }

    public sealed class ExpressionLayout(MemoryStructure structure, MemoryStructure operands)
    {
        public MemoryField OperandsField { get; } = structure.Get("Operands");

        public MemoryField OperandCountField { get; } = structure.Get("Operand Count");

        public MemoryField OperandField { get; } = operands.Get("Operand");
    }

    public sealed class ExecutionContextLayout(MemoryStructure structure)
    {
        public MemoryField QueryContextField { get; } = structure.Get("Query Execution Context");
    }

    public sealed class QueryContextLayout(MemoryStructure structure)
    {
        public MemoryField QueryScanField { get; } = structure.Get("Query Scan");
    }

    public sealed class QueryScanLayout(MemoryStructure structure)
    {
        public MemoryField ContextField { get; } = structure.Get("Query Scan Context");
    }

    public sealed class QueryScanContextLayout(MemoryStructure structure)
    {
        public MemoryField StatementField { get; } = structure.Get("Statement");
    }

    public sealed class StatementLayout(MemoryStructure structure)
    {
        public MemoryField RowContextField { get; } = structure.Get("Row Context");

        public MemoryField ExecutorField { get; } = structure.Get("Output Executor");
    }

    public sealed class RowContextLayout(MemoryStructure structure)
    {
        public MemoryField SlotField { get; } = structure.Get("Slot");
    }

    public sealed class HashValueLayout(MemoryStructure structure)
    {
        public MemoryField SlotField { get; } = structure.Get("Slot");

        public MemoryField RecordOffsetField { get; } = structure.Get("Record Offset");

        public MemoryField AccessorField { get; } = structure.Get("Accessor");
    }

    public sealed class StorageValueLayout(MemoryStructure structure)
    {
        public MemoryField SlotField { get; } = structure.Get("Slot");

        public MemoryField AccessorField { get; } = structure.Get("Accessor");
    }

    public sealed class SwitchValueLayout(MemoryStructure structure)
    {
        public MemoryField ReaderField { get; } = structure.Get("Reader");

        public MemoryField InputField { get; } = structure.Get("Input");
    }

    public sealed class CachedSwitchLayout(MemoryStructure structure)
    {
        public MemoryField ValueField { get; } = structure.Get("Value");

        public IReadOnlyList<MemoryField> EntryFields { get; } =
        [
            structure.Get("Cached Status"),
            structure.Get("Cached Type"),
            structure.Get("Cached Value"),
            structure.Get("Cached Length")
        ];

        public int EntryOffset => EntryFields[0].Offset;
    }

    public sealed class ExpressionValueLayout(MemoryStructure structure)
    {
        public MemoryField ExecutorField { get; } = structure.Get("Executor");
    }

    public sealed class ExecutorLayout(MemoryStructure structure, MemoryStructure values, MemoryStructure executors)
    {
        public MemoryField ValuesField { get; } = structure.Get("Input Values");

        public MemoryField ValueField { get; } = values.Get("Value");

        public MemoryField ElementField { get; } = executors.Get("Executor");
    }

    public sealed class ExecValueLayout(MemoryStructure structure)
    {
        public MemoryField EntryField { get; } = structure.Get("Entry");
    }

    public sealed class RowsetLayout(MemoryStructure structure)
    {
        public ulong InterfaceOffset { get; } = (ulong)structure.Get("Interface").Offset;

        public MemoryField RowField { get; } = structure.Get("Row");
    }

    public sealed class RowLayout(MemoryStructure structure)
    {
        public MemoryField RecordField { get; } = structure.Get("Record");
    }

    public sealed class RowBufferLayout(MemoryStructure structure)
    {
        public MemoryField EntryField { get; } = structure.Get("Entry");

        public MemoryField StatusField { get; } = structure.Get("Status");

        public MemoryField TypeField { get; } = structure.Get("Type");

        public MemoryField ValueField { get; } = structure.Get("Value");

        public MemoryField LengthField { get; } = structure.Get("Length");

        public IReadOnlyList<MemoryField> EntryFields => field ??= [StatusField, TypeField, ValueField, LengthField];
    }

    public sealed class SortIteratorLayout(MemoryStructure structure)
    {
        public MemoryField PushRowExecutorField { get; } = structure.Get("Push Row Executor");

        public MemoryField MemoryUsageField { get; } = structure.Get("Memory Usage");
    }

    public sealed class SortRowsetLayout(MemoryStructure structure)
    {
        public MemoryField DescriptorField { get; } = structure.Get("Sort Descriptor");

        public MemoryField RowCountField { get; } = structure.Get("Row Count");

        public MemoryField KeyColumnsField { get; } = structure.Get("Key Columns");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class SortDescriptorLayout(MemoryStructure structure)
    {
        public IReadOnlyList<MemoryField> RunFields { get; } = structure.GetAll("Run");

        public MemoryField CursorField { get; } = structure.Get("Cursor");

        public MemoryField CurrentListField { get; } = structure.Get("Current List");

        public IReadOnlyList<MemoryField> RunHeadFields => field ??= [.. RunFields, CursorField, CurrentListField];

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class SortEntryLayout(MemoryStructure structure)
    {
        public int Size { get; } = structure.Size;

        public MemoryField RecordField { get; } = structure.Get("Record");

        public MemoryField KeyField { get; } = structure.Get("Key");

        public MemoryField RecordLengthField { get; } = structure.Get("Record Length");

        public MemoryField KeyLengthField { get; } = structure.Get("Key Length");

        public MemoryField NextField { get; } = structure.Get("Next");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class HashGroupLayout(MemoryStructure structure)
    {
        public MemoryField IteratorField { get; } = structure.Get("Iterator");

        public MemoryField ColumnsField { get; } = structure.Get("Columns");

        public MemoryField ScanColumnsField { get; } = structure.Get("Scan Columns");

        public MemoryField ActivePartitionField { get; } = structure.Get("Active Partition");

        public MemoryField LastPartitionField { get; } = structure.Get("Last Partition");

        public MemoryField FirstPartitionField { get; } = structure.Get("First Partition");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class HashPartitionLayout(MemoryStructure structure)
    {
        private const int ActiveWorkfile = 1;

        public MemoryField GroupField { get; } = structure.Get("Group");

        public MemoryField RowCountField { get; } = structure.Get("Row Count");

        public MemoryField WorkfileField { get; } = structure.GetAll("Workfile Rowset")[ActiveWorkfile];

        public MemoryField DirectoryField { get; } = structure.Get("Directory");

        public MemoryField DirectoryCountField { get; } = structure.Get("Directory Count");

        public MemoryField BucketMaskField { get; } = structure.Get("Bucket Mask");

        public MemoryField PreviousPartitionField { get; } = structure.Get("Previous Partition");

        public MemoryField BitmapPagesField { get; } = structure.Get("Bitmap Pages");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class HashItemLayout(MemoryStructure structure)
    {
        private const int PageSize = 8192;

        public int Size { get; } = structure.Size;

        public int BucketsPerPage => PageSize / Size;

        public MemoryField CountField { get; } = structure.Get("Count");

        public MemoryField HeadField { get; } = structure.Get("Head");

        public MemoryField NextField { get; } = structure.Get("Next");

        public MemoryField KeyField { get; } = structure.Get("Key");

        public MemoryField RowField { get; } = structure.Get("Row");

        public IReadOnlyList<MemoryField> KeyFields { get; } = structure.GetAll("Key");

        public IReadOnlyList<MemoryField> RowFields { get; } = structure.GetAll("Row");

        public IReadOnlyList<MemoryField> Fields => field ??= [CountField, HeadField, NextField, KeyField, RowField];
    }

    public sealed class BufferLayout(MemoryStructure structure)
    {
        public int Size { get; } = structure.Size;

        public MemoryField FrameField { get; } = structure.Get("Frame (bpage)");

        public MemoryField DatabaseIdField { get; } = structure.Get("Database Id (bdbid)");

        public MemoryField StateHistoryField { get; } = structure.Get("State History (blog)");

        public MemoryField ReadTimeField { get; } = structure.Get("Read µs (bReadMicroSec)");

        public MemoryField ErrorCodeField { get; } = structure.Get("Error Code (berrcode)");

        public MemoryField LastUsedField { get; } = structure.Get("Last Used (bUse1)");

        public MemoryField StatusField { get; } = structure.Get("Status (bstat)");

        public MemoryField PageIdField { get; } = structure.Get("Page ID (bpageno)");

        public MemoryField FileIdField { get; } = structure.Get("File ID (bpageno)");

        public MemoryField PartitionField { get; } = structure.Get("Partition (bpart)");

        public MemoryField RecoveryUnitField { get; } = structure.Get("Recovery Unit (bpru)");

        public MemoryField LatchWaitersField { get; } = structure.Get("Latch Waiters");

        public MemoryField LatchOwnerField { get; } = structure.Get("Latch Owner");

        public MemoryField LatchStateField { get; } = structure.Get("Latch State");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class BatchLayout(MemoryStructure structure, MemoryStructure vector)
    {
        public int VectorSize { get; } = vector.Size;

        public MemoryField AllocationBitmapField { get; } = structure.Get("Allocation Bitmap");

        public MemoryField VectorArrayField { get; } = structure.Get("Vector Array");

        public MemoryField PureBitmapField { get; } = structure.Get("Pure Bitmap");

        public MemoryField VectorCountField { get; } = structure.Get("Vector Count");

        public MemoryField CapacityField { get; } = structure.Get("Capacity");

        public MemoryField ActiveRowCountField { get; } = structure.Get("Active Rows");

        public MemoryField SelectionField { get; } = structure.Get("Selection Vector");

        public MemoryField VectorBufferField { get; } = vector.Get("Buffer");

        public MemoryField CurrentDeepPageField { get; } = vector.Get("Current Deep Page");

        public MemoryField FirstDeepPageField { get; } = vector.Get("First Deep Page");

        public IReadOnlyList<MemoryField> Fields => structure.Fields;
    }

    public sealed class DeepDataPageLayout(MemoryStructure structure)
    {
        public int HeaderSize { get; } = structure.Size;

        public MemoryField NextPageField { get; } = structure.Get("Next Page");

        public MemoryField MagicField { get; } = structure.Get("Magic");

        public MemoryField CapacityField { get; } = structure.Get("Capacity");
    }

    public sealed class PageLocationLayout(MemoryStructure allocationPage, MemoryStructure pageHandle)
    {
        public MemoryField AllocationPageIdField { get; } = allocationPage.Get("Page Id");

        public MemoryField PageHandleBufferField { get; } = pageHandle.Get("Buffer");
    }
}
