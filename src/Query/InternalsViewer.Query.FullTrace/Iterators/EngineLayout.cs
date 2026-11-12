using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.Query.FullTrace.Iterators;

public static class EngineLayout
{
    private const string AnyIterator = "CQScan*";

    private static PlanNodeLayout PlanNode => PlanNodeLayout.SqlServer2025;

    private static MemoryField Pointer(string name, int offset, params string[] writers)
        => new(name, offset, sizeof(ulong)) { Writers = writers };

    private static MemoryField Number(string name, int offset, int length, params string[] writers)
        => new(name, offset, length) { Writers = writers };

    public static class Iterator
    {
        public static MemoryField InputsField { get; } = Pointer("Inputs", 0x08, AnyIterator);

        public static MemoryField InputField { get; } = Pointer("Input", 0x00, AnyIterator);

        public static MemoryField PlanObjectField { get; } = Pointer("Plan Object", (int)PlanNode.RowIteratorPlanObjectOffset);

        public static MemoryField ExecutionContextField { get; } =
            Pointer("Execution Context", 0x18, "::PqsWrapQScan", "CXte*", AnyIterator);
    }

    public static class ScanIterator
    {
        public static MemoryField RowsetField { get; } = Pointer("Rowset", 0x48, "OpenRowsetSS::OpenRowset");

        public static MemoryField PrefetchManagerField { get; } = Pointer("Prefetch Manager", 0x70);

        public static MemoryField RowBufferField { get; } = Pointer("Row Buffer",
                                                              (int)AccessorBindingLayout.ScanRowBufferOffset,
                                                              "CPrefetchMgr::CreateBuffer",
                                                              "CQScanRowsetNew::CQScanRowsetNew");

        public static MemoryField RowBindingsField { get; } = Pointer("Row Bindings", (int)AccessorBindingLayout.ScanBindingsOffset);

        public static MemoryField ClientRowField { get; } = Pointer("Client Row", 0x168);
    }

    public static class TopIterator
    {
        public static MemoryField TopCountField { get; } = Pointer("Top Count Expression", 0x90);
    }

    public static class HashIterator
    {
        private const string Creator = "CQScanHash::CreateInputAndOutput";

        public static MemoryField BuildOutputValuesField { get; } = Pointer("Build Output Values", 0x58, Creator);

        public static MemoryField ProbeOutputValuesField { get; } = Pointer("Probe Output Values", 0x60, Creator);

        public static MemoryField BuildInputValuesField { get; } = Pointer("Build Input Values", 0xC8, Creator);

        public static MemoryField ProbeInputValuesField { get; } = Pointer("Probe Input Values", 0xD0, Creator);

        public static MemoryField ValueField { get; } = Pointer("Value", 0x00, Creator);
    }

    public static class BatchAdapter
    {
        private const string Constructor = "CQScanBatchHelper::CQScanBatchHelper";

        private const string GetRow = "CQScanBatchHelper::GetRow";

        public static MemoryField OutputValuesField { get; } = Pointer("Output Values", 0x48, Constructor);

        public static MemoryField OutputValueField { get; } = Pointer("Output Value", 0x00, Constructor);

        public static MemoryField ChildField { get; } = Pointer("Child", 0x58, "CBpXte::CreateBatchIterators");

        public static MemoryField RowBufferField { get; } = Pointer("Row Buffer", 0x78, Constructor);

        public static MemoryField ColumnMapField { get; } = Pointer("Column Map", 0x80, Constructor);

        public static MemoryField CursorField { get; } = Number("Cursor", 0x88, sizeof(int), GetRow);

        public static MemoryField BatchField { get; } = Pointer("Batch", 0x90, GetRow);

        public static MemoryField ColumnMapVectorField { get; } = Number("Vector", 0x02, sizeof(ushort), "CBpQScan*");
    }

    public static class PlanObject
    {
        public static MemoryField NodeIdField { get; } = Number("Node Id", (int)PlanNode.PlanNodeIdOffset, sizeof(int));

        public static MemoryField InputsField { get; } = Pointer("Inputs", 0x18);

        public static MemoryField QueryField { get; } = Pointer("Query", (int)PlanNode.PlanQueryOffset);

        public static MemoryField OutputColumnsField { get; } = Pointer("Output Columns", (int)PlanNode.PlanOutputsOffset, "CXte::CXte");

        public static MemoryField TopCountField { get; } = Pointer("Top Count Expression", 0x88);

        public static MemoryField RowsetIdField { get; } = Pointer("Rowset Id", 0xB0);
    }

    public static class OutputList
    {
        public static MemoryField CountField { get; } = Number("Count", (int)PlanNode.OutputCountOffset, sizeof(int));

        public static MemoryField ColumnsField { get; } = Pointer("Columns", (int)PlanNode.OutputColumnsOffset, "CBndOutputs::CBndOutputs");

        public static MemoryField ColumnField { get; } =
            Pointer("Column", 0x00, "CXte::RemapOutputValrefs", "CBpXte::BpCompile", "CBndOutputs::Append");
    }

    public static class ColumnReference
    {
        public const ulong InterfaceOffset = 0x28;

        public static MemoryField ProducerField { get; } = Pointer("Producer", (int)PlanNode.ColumnProducerOffset, "CValRef*");

        public static MemoryField StorageProducerField { get; } =
            Pointer("Producer", (int)PlanNode.StorageColumnProducerOffset, "CValRefExecTable::CValRefExecTable");

        public static MemoryField JoinSideField { get; } = Number("Side", 0x58, sizeof(ushort));

        public static MemoryField JoinColumnField { get; } = Number("Column", 0x5C, sizeof(int));

        public static MemoryField OrdinalColumnField { get; } = Number("Column", 0x58, sizeof(int));

        public static MemoryField CalculatedProducerField { get; } = Pointer("Producer", 0x58);

        public static MemoryField ExpressionField { get; } = Pointer("Expression", 0x68);

        public static MemoryField RedirectProducerField { get; } =
            Pointer("Producer", (int)PlanNode.RedirectProducerOffset, "CValRefBatchRedirect::CValRefBatchRedirect");

        public static MemoryField RedirectColumnField { get; } = Number("Column", (int)PlanNode.RedirectColumnOffset, sizeof(int));
    }

    public static class Expression
    {
        public static MemoryField OperandsField { get; } = Pointer("Operands", 0x28);

        public static MemoryField OperandCountField { get; } = Number("Operand Count", 0x30, sizeof(int));

        public static MemoryField OperandField { get; } = Pointer("Operand", 0x00);
    }

    public static class ExecutionContext
    {
        public static MemoryField QueryContextField { get; } = Pointer("Query Execution Context", 0x1B8, "CQueryScan::Init");
    }

    public static class QueryContext
    {
        private const string Constructor = "CQueryExecContext::CQueryExecContext";

        public static MemoryField QueryScanField { get; } = Pointer("Query Scan", 0x20, Constructor);

        public static MemoryField IteratorMapField { get; } = Pointer("Iterator Map", 0x290, Constructor);
    }

    public static class IteratorMap
    {
        public static MemoryField BucketField { get; } = Pointer("Bucket", 0x00, AnyIterator);

        public static MemoryField NextField { get; } = Pointer("Next", 0x00, AnyIterator);

        public static MemoryField PlanObjectField { get; } = Pointer("Plan Object", 0x08, AnyIterator);

        public static MemoryField IteratorField { get; } = Pointer("Iterator", 0x10, AnyIterator);
    }

    public static class QueryScan
    {
        public static MemoryField ContextField { get; } = Pointer("Query Scan Context", 0x58, "CQueryScan::CQueryScan");
    }

    public static class QueryScanContext
    {
        public static MemoryField StatementField { get; } = Pointer("Statement", 0x28, "CQECtxt::Init");
    }

    public static class Statement
    {
        public static MemoryField RowContextField { get; } = Pointer("Row Context", 0xD8, "CXStmtQuery::AllocateMembers");
    }

    public static class RowContext
    {
        public static MemoryField SlotField { get; } =
            Pointer("Slot", 0x00, "RowsetNewSS::FetchNextRow", "CHashWorkfilePartitionGroup::*");
    }

    public static class HashValue
    {
        public static MemoryField SlotField { get; } =
            Number("Slot", TraceHashTableLayout.ValueSlotOffset, sizeof(int), "CValHash::CValHash");

        public static MemoryField RecordOffsetField { get; } = Number("Record Offset", TraceHashTableLayout.ValueRecordOffset, sizeof(int));
    }

    public static class StorageValue
    {
        public static MemoryField SlotField { get; } = Number("Slot", 0x10, sizeof(int), "CQScanRowsetNew::CreateValSSTable");

        public static MemoryField AccessorField { get; } = Pointer("Accessor", 0x18);
    }

    public static class SwitchValue
    {
        public static MemoryField ReaderField { get; } = Pointer("Reader",
                                                           TraceHashTableLayout.SwitchPrimaryOffset,
                                                           "CQScanHash::SetupPartitionInputsAndOutputs",
                                                           "CQScanHash::CreateInputAndOutput");

        public static MemoryField InputField { get; } =
            Pointer("Input", TraceHashTableLayout.SwitchInputOffset, "CQScanHash::CreateInputAndOutput");
    }

    public static class CachedSwitch
    {
        public static MemoryField ValueField { get; } = Pointer("Value", 0x08, "CQScanHash::SetupPartitionInputsAndOutputs");
    }

    public static class ExecValue
    {
        public static MemoryField EntryField { get; } = Pointer("Entry", 0x08, "CValRefOrdinal::PvalInvokeSpecific");
    }

    public static class Rowset
    {
        public const ulong InterfaceOffset = 0x70;

        public static MemoryField RowField { get; } = Pointer("Row", 0xB8, "RowsetNewSS::FetchNextRow");
    }

    public static class Row
    {
        public static MemoryField RecordField { get; } = Pointer("Record", 0x50, "IndexRowScanner::*");
    }

    public static class RowBuffer
    {
        public static MemoryField EntryField { get; } = Number("Entry", 0x00, 0x20);
    }

    public static class Batch
    {
        public static MemoryField VectorsField { get; } = Pointer("Vectors", (int)BatchReader.VectorArrayOffset, "CBpBatch::*");

        public static MemoryField SelectionField { get; } = Pointer("Selection", (int)BatchReader.SelectionOffset, "CBpBatch::*");

        public static MemoryField VectorField { get; } = Pointer("Vector", 0x00, "CBpBatch::*");

        public static MemoryField SelectedRowField { get; } = Number("Row", 0x00, sizeof(ushort));

        public static MemoryField VectorSlotField { get; } = Pointer("Row", 0x00);
    }

    public static class SortIterator
    {
        public static MemoryField RowsetField { get; } = Pointer("Rowset", 0x48, "::CreateRowsetSort");

        public static MemoryField MemoryUsageField { get; } = Pointer("Memory Usage", 0x208, "CQScanSortNew::CQScanSortNew");

        public static IReadOnlyList<MemoryField> Fields { get; } =
        [
            Iterator.InputsField,
            Iterator.PlanObjectField,
            Iterator.ExecutionContextField,
            RowsetField,
            ScanIterator.RowBufferField,
            ScanIterator.RowBindingsField,
            MemoryUsageField
        ];
    }

    public static class SortRowset
    {
        public static MemoryField DescriptorField { get; } = Pointer("Sort Descriptor", 0x18, "RowsetSorted::CreateSort");

        public static MemoryField RowCountField { get; } = Number("Row Count", 0x98, sizeof(int), "::soAllocRecBuf");

        public static MemoryField KeyColumnsField { get; } = Pointer("Key Columns", 0xB8, "::build_sort_collist");

        public static IReadOnlyList<MemoryField> Fields { get; } = [DescriptorField, RowCountField, KeyColumnsField];
    }

    public static class SortDescriptor
    {
        public const int RunCount = 32;

        private const string Initialiser = "::sort_init";

        private const string Linker = "::lmlink";

        private const string Merger = "::lmAddCurToList";

        private const string Sorter = "::lmsort";

        private const int RunOffset = 0x70;

        public static MemoryField ParametersField { get; } = Pointer("Sort Parameters", 0x08, Initialiser);

        public static MemoryField KeyColumnsField { get; } = Pointer("Key Columns", 0x10, Initialiser);

        public static MemoryField MemoryUsageField { get; } = Pointer("Memory Usage", 0x20, Initialiser);

        public static MemoryField EstimatedRowsField { get; } = Number("Estimated Rows", 0x50, sizeof(int), Initialiser);

        public static MemoryField RecordLengthField { get; } = Number("Fixed Record Length", 0x58, sizeof(int), Initialiser);

        public static MemoryField StateField { get; } = Number("State", 0x68, sizeof(int), "::sort_endinput");

        public static IReadOnlyList<MemoryField> RunFields { get; } =
        [
            .. Enumerable.Range(0, RunCount).Select(i => Pointer($"Run {i}", RunOffset + i * sizeof(ulong), Merger, Sorter))
        ];

        public static MemoryField CursorField { get; } = Pointer("Cursor", 0x170, Sorter, "::lmnext");

        public static MemoryField CurrentListField { get; } = Pointer("Current List", 0x178, Linker, Merger);

        public static MemoryField CurrentListTailField { get; } = Pointer("Current List Tail", 0x180, Linker, Merger);

        public static MemoryField CurrentListCountField { get; } = Number("Current List Count", 0x188, sizeof(int), Linker, Merger);

        public static MemoryField EntrySizeField { get; } = Number("Entry Size", 0x200, sizeof(int), Initialiser);

        public static MemoryField LinkedCountField { get; } = Number("Linked Entries", 0x208, sizeof(int), Linker, Sorter);

        public static IReadOnlyList<MemoryField> RunHeadFields { get; } = [.. RunFields, CursorField, CurrentListField];

        public static IReadOnlyList<MemoryField> Fields { get; } =
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
    }

    public static class SortEntry
    {
        public const int Size = 0x28;

        private const string Linker = "::lmlink";

        public static MemoryField RecordField { get; } = Pointer("Record", 0x08, Linker);

        public static MemoryField KeyField { get; } = Pointer("Key", 0x10, Linker);

        public static MemoryField RecordLengthField { get; } = Number("Record Length", 0x18, sizeof(ushort), Linker);

        public static MemoryField KeyLengthField { get; } = Number("Key Length", 0x1A, sizeof(ushort), Linker);

        public static MemoryField NextField { get; } = Pointer("Next", 0x20, Linker, "::lmsort");

        public static IReadOnlyList<MemoryField> Fields { get; } = [RecordField, KeyField, RecordLengthField, KeyLengthField, NextField];
    }
}
