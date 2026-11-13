using System.Collections.Concurrent;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using RowColumnBinding = InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.ColumnBinding;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

internal sealed class PlanOutputLinkReader(TraceResult trace)
{
    private const string GetRowMethod = "GetRow";

    private const int InstanceArgument = 0;

    private ConcurrentDictionary<(int NodeId, ulong Reference), PlanOutputPath> Cache { get; } = new();

    private ConcurrentDictionary<ulong, IReadOnlyList<PlanOutputLink>> StorageCache { get; } = new();

    private Dictionary<ulong, TraceArgumentCall> FirstGetRows => field ??= GetFirstGetRows();

    private Dictionary<ulong, (TracePosition From, TracePosition To)> RowRanges => field ??= GetRowRanges();

    private Dictionary<ulong, SqlTypeInfo> ReferenceTypes => field ??= GetReferenceTypes();

    public PlanOutputPath Read(int nodeId, PlanOutputColumn column)
        => Cache.GetOrAdd((nodeId, column.Reference), _ => Find(nodeId, column));

    public void ReadAll()
    {
        foreach (var (nodeId, columns) in trace.OutputColumns)
        {
            foreach (var column in columns)
            {
                Read(nodeId, column);
            }
        }
    }

    public Dictionary<int, PlanOutputRoute> ReadDataPath(IReadOnlyList<PlanOutputRouteOperator> operators, bool isPlanObjectIncluded)
    {
        var storage = new Dictionary<int, IReadOnlyList<PlanOutputLink>>();

        foreach (var found in operators)
        {
            storage.TryAdd(found.NodeId, StorageCache.GetOrAdd(found.Iterator, i => ReadAt(i, r => r.AddStorage(i))));
        }

        return PlanOutputRoute.Create(operators, storage, GetBlockStart, isPlanObjectIncluded);
    }

    public string? GetUnexpectedWriter(PlanOutputLink link)
        => link.Field is { Writers.Count: > 0 } field && GetWriter(link, field) is { } writer && !field.IsWrittenBy(writer)
            ? writer
            : null;

    private static string? GetFunctionName(CallTreeNode? node)
    {
        while (node?.Frame?.Function is { MethodName: "memset" or "memcpy" or "memmove" })
        {
            node = node.Parent;
        }

        return node?.Frame?.Function is { } writer ? $"{writer.ClassName}::{writer.MethodName}" : null;
    }

    private static bool IsCurrentValue(MemoryHistoryStore store, MemoryHistoryWrite write, int offset, PlanOutputLink link, int length)
    {
        var count = Math.Min(Math.Min(length, sizeof(ulong)), write.Offset + write.Length - offset);

        Span<byte> written = stackalloc byte[count];

        Span<byte> current = stackalloc byte[count];

        store.CopyBytes(write, offset - write.Offset, written);

        return new MemoryHistoryReader(store, link.Position).Read(link.Source + (ulong)link.SourceOffset, current) == count
               && written.SequenceEqual(current);
    }

    private string? GetWriter(PlanOutputLink link, MemoryField field)
    {
        var store = trace.MemoryHistory;

        var block = store.GetBlock(link.Source, link.Position);

        if (block < 0)
        {
            return null;
        }

        var info = store.Blocks[block];

        var offset = (int)(link.Source - info.Address) + link.SourceOffset;

        var start = store.GetInstance(block, link.Position) is >= 0 and var instance
            ? store.GetInstances(block)[instance].FromPosition
            : info.FromPosition;

        var writes = store.GetWrites(block);

        for (var index = store.GetWriteCount(block, link.Position) - 1; index >= 0 && writes[index].Position >= start; index--)
        {
            var write = writes[index];

            if (write.Type != MemoryHistoryAccessType.Read && write.Offset <= offset && offset < write.Offset + write.Length)
            {
                return IsCurrentValue(store, write, offset, link, field.Length)
                    ? GetFunctionName(trace.CallStore.GetNode(write.CallNodeIndex))
                    : null;
            }
        }

        return null;
    }

    private IReadOnlyList<PlanOutputLink> ReadAt(ulong iterator, Action<Route> add)
    {
        if (iterator == 0 || (FirstGetRows.GetValueOrDefault(iterator) ?? FirstGetRows.Values.MaxBy(c => c.ToPosition)) is not { } call)
        {
            return [];
        }

        var position = call.ToPosition != TracePosition.Unknown ? call.ToPosition : call.FromPosition;

        var route = CreateRoute(new MemoryHistoryReader(trace.MemoryHistory, position), position);

        add(route);

        return route.Links;
    }

    private ulong GetBlockStart(PlanOutputLink link)
        => trace.MemoryHistory.GetBlock(link.Address, link.Position) is >= 0 and var block ? trace.MemoryHistory.Blocks[block].Address : 0;

    private PlanOutputPath Find(int nodeId, PlanOutputColumn column)
    {
        var planNodes = trace.LinkedPlanNodes.Where(p => p.NodeId == nodeId).ToList();

        var call = planNodes.Concat(trace.LinkedPlanNodes.Where(p => p.NodeId == column.ProducerNodeId))
                            .Select(p => FirstGetRows.GetValueOrDefault(p.Iterator))
                            .FirstOrDefault(c => c is not null)
                   ?? FirstGetRows.Values.MaxBy(c => c.ToPosition);

        if (planNodes.Count == 0 || call is null)
        {
            return PlanOutputPath.Empty;
        }

        var position = call.ToPosition != TracePosition.Unknown ? call.ToPosition : call.FromPosition;

        var route = CreateRoute(new MemoryHistoryReader(trace.MemoryHistory, position), position);

        route.AddOutput(planNodes[0].PlanObject, planNodes[0].Iterator, column.Reference);

        var path = route.GetPath();

        foreach (var stage in path.Stages)
        {
            stage.ColumnType = stage.Consumer < 0
                ? column.Type
                : ReferenceTypes.TryGetValue(stage.Reference, out var type) ? type : null;
        }

        return path;
    }

    private Route CreateRoute(ITraceMemoryReader memory, TracePosition position)
        => new(memory, position, a => GetClassName(a, position), a => GetSize(a, position), trace.LinkedPlanNodes);

    private Dictionary<ulong, SqlTypeInfo> GetReferenceTypes()
    {
        var types = new Dictionary<ulong, SqlTypeInfo>();

        foreach (var columns in trace.OutputColumns.Values)
        {
            foreach (var column in columns)
            {
                types.TryAdd(column.Reference, column.Type);
            }
        }

        return types;
    }

    private int GetSize(ulong address, TracePosition position)
        => trace.MemoryHistory.GetBlock(address, position) is >= 0 and var block && trace.MemoryHistory.Blocks[block].Address == address
            ? trace.MemoryHistory.Blocks[block].Size
            : 0;

    private string? GetClassName(ulong address, TracePosition position)
        => trace.MemoryHistory.GetBlock(address, position) is >= 0 and var block
           && trace.MemoryHistory.Blocks[block] is var info
           && info.Address == address
            ? trace.AddressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition)
            : null;

    public (TracePosition From, TracePosition To)? GetRowRange(ulong iterator)
        => RowRanges.TryGetValue(iterator, out var range) ? range : null;

    private Dictionary<ulong, (TracePosition From, TracePosition To)> GetRowRanges()
    {
        var ranges = new Dictionary<ulong, (TracePosition From, TracePosition To)>();

        foreach (var list in trace.CallStore.GetCallLists())
        {
            if (list.Count == 0 || trace.CallStore.GetFirstNode(list)?.Frame?.Function?.MethodName != GetRowMethod)
            {
                continue;
            }

            var from = list.GetFromPosition(0);

            var to = list.GetToPosition(list.Count - 1);

            for (var index = 0; index < list.Count; index++)
            {
                if (list.GetToPosition(index) is { IsKnown: true } end && end > to)
                {
                    to = end;
                }
            }

            ranges[list.InstanceAddress] = ranges.TryGetValue(list.InstanceAddress, out var found)
                ? (from < found.From ? from : found.From, to > found.To ? to : found.To)
                : (from, to);
        }

        return ranges;
    }

    private Dictionary<ulong, TraceArgumentCall> GetFirstGetRows()
    {
        var calls = new Dictionary<ulong, TraceArgumentCall>();

        foreach (var list in trace.CallStore.GetCallLists())
        {
            if (trace.CallStore.GetFirstNode(list)?.Frame?.Function?.MethodName != GetRowMethod)
            {
                continue;
            }

            foreach (var call in list)
            {
                var iterator = call.GetSlot(InstanceArgument);

                if (!calls.TryGetValue(iterator, out var first) || call.FromPosition < first.FromPosition)
                {
                    calls[iterator] = call;
                }
            }
        }

        return calls;
    }

    internal sealed class Route(ITraceMemoryReader memory,
                                TracePosition position,
                                Func<ulong, string?> getClassName,
                                Func<ulong, int> getSize,
                                IReadOnlyList<IteratorPlanNode> planNodes)
    {
        private const ulong PointerSize = sizeof(ulong);

        private const int MaximumEntries = 64;

        private const int MaximumOutputs = 4096;

        private const string JoinReferenceClass = "CValRefJoin";

        private const string OrdinalReferenceClass = "CValRefOrdinal";

        private const string StorageReferenceClass = "CValRefSS";

        private const string CalculatedReferenceClass = "CValRefCalculated";

        private const string ReferenceClassPrefix = "CValRef";

        private const string BatchIteratorPrefix = "CBpQScan";

        private const string BatchRedirectClass = "CValRefBatchRedirect";

        private const string AdapterClass = "CQScanBatchHelper";

        private const string StreamAggregateClass = "CXteStreamAggregate";

        private const string SortIteratorClass = "CQScanSortNew";

        private const string StreamAggregateIteratorClass = "CQScanStreamAggregateNew";

        private const string StorageRowsetClass = "RowsetNewSS";

        public List<PlanOutputLink> Links { get; } = [];

        public List<PlanOutputStage> Stages { get; } = [];

        private EngineLayout Layout { get; } = memory.Layout;

        private MemoryField[] OutputValueFields { get; } =
        [
            memory.Layout.HashIterator.BuildOutputValuesField,
            memory.Layout.HashIterator.ProbeOutputValuesField
        ];

        private MemoryField[] InputValueFields { get; } =
        [
            memory.Layout.HashIterator.BuildInputValuesField,
            memory.Layout.HashIterator.ProbeInputValuesField
        ];

        private ulong BufferEntrySize { get; } = (ulong)memory.Layout.RowBuffer.EntryField.Length;

        private int StageIndex { get; set; } = -1;

        private PlanOutputStage Stage => Stages[StageIndex];

        private Dictionary<ulong, ulong> Iterators => field ??= GetIterators();

        public void AddOutput(ulong planObject, ulong iterator, ulong reference)
        {
            if (!TryRead(planObject, Layout.PlanObject.OutputColumnsField, out ulong outputs)
                || !TryRead(outputs, Layout.OutputList.CountField, out int count)
                || !TryRead(outputs, Layout.OutputList.ColumnsField, out ulong array))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputList, outputs, planObject, Layout.PlanObject.OutputColumnsField, 0, isRoute: true);

            Add(PlanOutputLinkType.OutputArray, array, outputs, Layout.OutputList.ColumnsField, 0, isRoute: true);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumOutputs); index++)
            {
                var columnOffset = index * PointerSize;

                var columnField = Layout.OutputList.ColumnField;

                if (TryRead(array + columnOffset, columnField, out ulong found) && found == reference)
                {
                    Add(PlanOutputLinkType.Reference, reference, array, columnField, 0, columnOffset, isRoute: true);

                    var owner = GetIterator(planObject, 0, false) ?? iterator;

                    AddContext(owner);

                    AddReference(reference, owner, 0);

                    return;
                }
            }
        }

        public PlanOutputPath GetPath() => new() { Links = Links, Stages = Stages };

        public void AddStorage(ulong iterator)
        {
            StageIndex = Stages.Count;

            Stages.Add(new PlanOutputStage(0, 0, -1));

            AddContext(iterator);

            var className = getClassName(iterator);

            if (IteratorLayout.IsRowset(className))
            {
                var bindings = ReadRowBindings(iterator);

                AddRowBufferStorage(iterator, Layout.RowsetIterator.RowBufferField, bindings?.Bound);

                if (bindings is { } found)
                {
                    AddRowBindingStorage(iterator, found);
                }
            }

            if (IteratorLayout.IsRowset(className)
                && TryRead(iterator, Layout.RowsetIterator.RowsetField, out ulong rowset)
                && rowset >= TraceAddress.MinimumAddress + Layout.Rowset.InterfaceOffset
                && getClassName(rowset - Layout.Rowset.InterfaceOffset) == StorageRowsetClass)
            {
                Add(PlanOutputLinkType.Rowset, rowset, iterator, Layout.RowsetIterator.RowsetField, 0);

                var rowField = Layout.Rowset.RowField;

                AddRecord(rowset, rowField with { Offset = rowField.Offset - (int)Layout.Rowset.InterfaceOffset }, 0);
            }

            if (IteratorLayout.IsHash(className))
            {
                var valueField = Layout.HashIterator.ValueField;

                foreach (var field in OutputValueFields)
                {
                    AddValueArray(iterator, field, PlanOutputLinkType.OutputValues, PlanOutputLinkType.Value, valueField);
                }

                foreach (var field in InputValueFields)
                {
                    AddValueArray(iterator, field, PlanOutputLinkType.InputValues, PlanOutputLinkType.InputValue, valueField);
                }
            }

            if (className == StreamAggregateIteratorClass)
            {
                AddAggregateStorage(iterator);

                AddExecutor(iterator, Layout.StreamAggregateIterator.AggregateExecutorField);

                AddExecutorArray(iterator, Layout.StreamAggregateIterator.CompareExecutorsField);

                AddExecutorArray(iterator, Layout.StreamAggregateIterator.MoveExecutorsField);
            }

            if (className == SortIteratorClass)
            {
                AddExecutor(iterator, Layout.SortIterator.PushRowExecutorField);
            }

            if (className == AdapterClass)
            {
                var adapterValues = Layout.BatchAdapter.OutputValuesField;

                var adapterValue = Layout.BatchAdapter.OutputValueField;

                AddValueArray(iterator, adapterValues, PlanOutputLinkType.OutputValues, PlanOutputLinkType.Value, adapterValue);

                AddRowBufferStorage(iterator, Layout.BatchAdapter.RowBufferField, null);
            }
        }

        private ulong GetFieldAddress(ulong holder, MemoryField field, ulong elementOffset = 0)
            => holder + (ulong)field.Offset + elementOffset;

        private ulong? AddReference(ulong reference, ulong owner, int depth)
        {
            var consumer = StageIndex;

            StageIndex = Stages.Count;

            Stages.Add(new PlanOutputStage(reference, depth, consumer));

            var entry = AddStage(reference, owner, depth);

            StageIndex = consumer;

            return entry;
        }

        private ulong? AddStage(ulong reference, ulong owner, int depth)
        {
            var className = getClassName(reference);

            Stage.ReferenceClass = className;

            if (className == CalculatedReferenceClass)
            {
                AddExpression(reference, owner, depth);

                return null;
            }

            if (className == BatchRedirectClass)
            {
                AddBatchColumn(reference, depth);

                return null;
            }

            var producerField = className == StorageReferenceClass
                ? Layout.ColumnReference.StorageProducerField
                : Layout.ColumnReference.ProducerField;

            if (!TryRead(reference, producerField, out ulong producer) || getClassName(producer) is null)
            {
                return null;
            }

            Add(PlanOutputLinkType.Producer, producer, reference, producerField, depth, isRoute: true);

            Stage.PlanObject = producer;

            if (GetIterator(producer, depth) is not { } producerIterator)
            {
                return null;
            }

            Stage.Iterator = producerIterator;

            if (IsBatch(owner) || IsBatch(producerIterator))
            {
                return null;
            }

            if (className == JoinReferenceClass)
            {
                AddJoinValue(reference, producerIterator, depth);

                return null;
            }

            if (className == StorageReferenceClass)
            {
                SetStorageRecord(reference, producerIterator, depth);

                return null;
            }

            if (className == OrdinalReferenceClass && getClassName(producer) == StreamAggregateClass)
            {
                return AddAggregateValue(reference, producer, producerIterator, depth);
            }

            return className == OrdinalReferenceClass ? AddBufferEntry(reference, producerIterator, depth) : null;
        }

        private void AddExpression(ulong reference, ulong owner, int depth)
        {
            var producerField = Layout.ColumnReference.CalculatedProducerField;

            if (TryRead(reference, producerField, out ulong producer) && getClassName(producer) is not null)
            {
                Add(PlanOutputLinkType.Producer, producer, reference, producerField, depth, isRoute: true);

                Stage.PlanObject = producer;

                Stage.Iterator = GetIterator(producer, depth) ?? 0;
            }

            if (!TryRead(reference, Layout.ColumnReference.ExpressionField, out ulong expression)
                || !TryRead(expression, Layout.Expression.OperandsField, out ulong operands)
                || !TryRead(expression, Layout.Expression.OperandCountField, out int count))
            {
                return;
            }

            Add(PlanOutputLinkType.Expression, expression, reference, Layout.ColumnReference.ExpressionField, depth, isRoute: true);

            SetStage(PlanOutputStageType.Expression, expression);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumEntries); index++)
            {
                var operandOffset = index * PointerSize;

                if (!TryRead(operands + operandOffset, Layout.Expression.OperandField, out ulong operand))
                {
                    continue;
                }

                var interfaceStart = operand - Layout.ColumnReference.InterfaceOffset;

                var input = IsReference(interfaceStart) ? interfaceStart : operand;

                if (!IsReference(input))
                {
                    continue;
                }

                Add(PlanOutputLinkType.Input, input, operands, Layout.Expression.OperandField, depth, operandOffset, isRoute: true);

                AddReference(input, owner, depth + 1);
            }
        }

        private void AddBatchColumn(ulong reference, int depth)
        {
            var producerField = Layout.ColumnReference.RedirectProducerField;

            if (!TryRead(reference, producerField, out ulong planObject)
                || !TryRead(reference, Layout.ColumnReference.RedirectColumnField, out int column)
                || column is < 0 or >= MaximumOutputs)
            {
                return;
            }

            Add(PlanOutputLinkType.Producer, planObject, reference, producerField, depth, isRoute: true);

            Stage.PlanObject = planObject;

            if (FindAdapter(planObject) is not { } adapter)
            {
                return;
            }

            Add(PlanOutputLinkType.Iterator, adapter, planObject, null, depth, isRoute: true);

            Stage.Iterator = adapter;

            if (TryRead(adapter, Layout.BatchAdapter.ChildField, out ulong child) && child >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.Iterator, child, adapter, Layout.BatchAdapter.ChildField, depth);
            }

            var columnOffset = (ulong)column * PointerSize;

            if (TryRead(adapter, Layout.BatchAdapter.OutputValuesField, out ulong values)
                && TryRead(values + columnOffset, Layout.BatchAdapter.OutputValueField, out ulong value))
            {
                Add(PlanOutputLinkType.OutputValues, values, adapter, Layout.BatchAdapter.OutputValuesField, depth);

                Add(PlanOutputLinkType.Value, value, values, Layout.BatchAdapter.OutputValueField, depth, columnOffset);
            }

            var entryOffset = (ulong)column * BufferEntrySize;

            if (TryRead(adapter, Layout.BatchAdapter.RowBufferField, out ulong rowBuffer) && rowBuffer >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.RowBuffer, rowBuffer, adapter, Layout.BatchAdapter.RowBufferField, depth);

                var entryField = Layout.RowBuffer.EntryField;

                Add(PlanOutputLinkType.BufferEntry, rowBuffer + entryOffset, rowBuffer, entryField, depth, entryOffset);

                SetStage(PlanOutputStageType.RowBuffer, rowBuffer);

                SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, rowBuffer + entryOffset, [], null));
            }
        }

        private ulong? FindAdapter(ulong planObject)
        {
            foreach (var iterator in planNodes.Select(n => n.Iterator))
            {
                if (getClassName(iterator) == AdapterClass && Read(iterator, Layout.Iterator.PlanObjectField) == planObject)
                {
                    return iterator;
                }
            }

            return null;
        }

        private void SetStorageRecord(ulong reference, ulong iterator, int depth)
        {
            if (Stage.Record is not null
                || !TryRead(reference, Layout.ColumnReference.StorageColumnIdField, out int columnId)
                || columnId <= 0)
            {
                return;
            }

            ulong[] chain =
            [
                (ulong)Layout.RowsetIterator.RowsetField.Offset,
                (ulong)Layout.Rowset.RowField.Offset - Layout.Rowset.InterfaceOffset,
                (ulong)Layout.Row.RecordField.Offset
            ];

            Stage.Record = new PlanOutputValueSource(PlanOutputLinkType.Record, iterator, chain, null, columnId);

            Stage.Type = PlanOutputStageType.Record;

            if (TryRead(iterator, Layout.RowsetIterator.RowsetField, out ulong rowset)
                && rowset >= TraceAddress.MinimumAddress + Layout.Rowset.InterfaceOffset)
            {
                Add(PlanOutputLinkType.Rowset, rowset, iterator, Layout.RowsetIterator.RowsetField, depth);

                AddRecord(rowset - Layout.Rowset.InterfaceOffset, Layout.Rowset.RowField, depth);
            }
        }

        private void AddRecord(ulong rowset, MemoryField? rowField, int depth)
        {
            if (!memory.TryRead(rowset + (ulong)(rowField?.Offset ?? 0), out ulong row) || row < TraceAddress.MinimumAddress)
            {
                return;
            }

            Add(PlanOutputLinkType.Row, row, rowset, rowField, depth);

            var recordField = Layout.Row.RecordField;

            Add(PlanOutputLinkType.Record, GetFieldAddress(row, recordField), row, recordField, depth);
        }

        private bool IsBatch(ulong iterator) => getClassName(iterator)?.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal) == true;

        private bool IsReference(ulong address)
            => getClassName(address)?.StartsWith(ReferenceClassPrefix, StringComparison.Ordinal) == true;

        private bool AddContext(ulong iterator)
        {
            if (IsBatch(iterator)
                || !TryRead(iterator, Layout.Iterator.ExecutionContextField, out ulong context)
                || !TryRead(context, Layout.ExecutionContext.QueryContextField, out ulong queryContext))
            {
                return false;
            }

            Add(PlanOutputLinkType.ExecutionContext, context, iterator, Layout.Iterator.ExecutionContextField, 0, isRoute: true);

            Add(PlanOutputLinkType.QueryContext, queryContext, context, Layout.ExecutionContext.QueryContextField, 0, isRoute: true);

            return true;
        }

        private ulong? GetIterator(ulong planObject, int depth, bool isLinked = true)
        {
            if (!Iterators.TryGetValue(planObject, out var iterator))
            {
                return null;
            }

            if (isLinked)
            {
                Add(PlanOutputLinkType.Iterator, iterator, planObject, null, depth, isRoute: true);
            }

            return iterator;
        }

        private Dictionary<ulong, ulong> GetIterators()
        {
            var iterators = new Dictionary<ulong, ulong>();

            var unnamed = new List<IteratorPlanNode>();

            foreach (var planNode in planNodes)
            {
                if (planNode.IsBatchHelper)
                {
                    continue;
                }

                var className = getClassName(planNode.Iterator);

                if (className is null)
                {
                    unnamed.Add(planNode);
                }
                else if (!IteratorSymbolLookup.IsProfilingClass(className))
                {
                    iterators.TryAdd(planNode.PlanObject, planNode.Iterator);
                }
            }

            foreach (var planNode in unnamed)
            {
                iterators.TryAdd(planNode.PlanObject, planNode.Iterator);
            }

            return iterators;
        }

        private void AddJoinValue(ulong reference, ulong iterator, int depth)
        {
            if (!TryRead(reference, Layout.ColumnReference.JoinSideField, out ushort side)
                || side >= OutputValueFields.Length
                || !TryRead(reference, Layout.ColumnReference.JoinColumnField, out int column)
                || column is < 0 or >= MaximumOutputs)
            {
                return;
            }

            var valuesField = OutputValueFields[side];

            var valueOffset = (ulong)column * PointerSize;

            if (!TryRead(iterator, valuesField, out ulong values)
                || !TryRead(values + valueOffset, Layout.HashIterator.ValueField, out ulong value))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputValues, values, iterator, valuesField, depth);

            Add(PlanOutputLinkType.Value,
                value,
                values,
                Layout.HashIterator.ValueField,
                depth,
                valueOffset,
                className: getClassName(value));

            AddValue(value, iterator, depth);
        }

        private void AddValue(ulong value, ulong iterator, int depth)
        {
            if (AddValueReaders(value, depth) is not { } found)
            {
                return;
            }

            var (primary, readers) = found;

            var inputField = Layout.SwitchValue.InputField;

            if (!TryRead(value, inputField, out ulong input) || !IsReference(input))
            {
                return;
            }

            Add(PlanOutputLinkType.Input, input, value, inputField, depth, isRoute: true);

            var entry = AddReference(input, iterator, depth + 1);

            if (readers.Count == 1
                && entry is { } inputEntry
                && TryRead(primary, Layout.CachedSwitch.ValueField, out ulong inner)
                && Read(inner, Layout.ExecValue.EntryField) == inputEntry)
            {
                Add(PlanOutputLinkType.InputValue, inner, primary, Layout.CachedSwitch.ValueField, depth);

                Add(PlanOutputLinkType.BufferEntry, inputEntry, inner, Layout.ExecValue.EntryField, depth);

                if (Stage.Type == PlanOutputStageType.None)
                {
                    SetStage(PlanOutputStageType.InputValue, inner);
                }

                SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, inputEntry, [], null));
            }

            AddInputValue(iterator, entry, readers, depth + 1);
        }

        private (ulong Primary, List<ulong> Readers)? AddValueReaders(ulong value, int depth)
        {
            if (ReadReader(value) is { } self)
            {
                Links[^1] = Links[^1] with { Slot = self.Slot, Offset = self.Offset };

                AddRow(value, self, depth);

                return null;
            }

            var readers = new List<ulong>();

            var readerField = Layout.SwitchValue.ReaderField;

            if (!TryRead(value, readerField, out ulong primary))
            {
                return null;
            }

            if (AddReader(primary, value, readerField, depth, readers) is { } reader)
            {
                AddRow(readers[^1], reader, depth);
            }

            return (primary, readers);
        }

        private void AddValueArray(ulong iterator,
                                   MemoryField field,
                                   PlanOutputLinkType arrayType,
                                   PlanOutputLinkType entryType,
                                   MemoryField entryField)
        {
            if (!TryRead(iterator, field, out ulong values) || values < TraceAddress.MinimumAddress)
            {
                return;
            }

            var count = Math.Min(getSize(values) / (int)PointerSize, MaximumEntries);

            if (count == 0)
            {
                return;
            }

            Add(arrayType, values, iterator, field, 0);

            for (var index = 0; index < count; index++)
            {
                var valueOffset = (ulong)index * PointerSize;

                if (!TryRead(values + valueOffset, entryField, out ulong value) || value < TraceAddress.MinimumAddress)
                {
                    continue;
                }

                var className = getClassName(value);

                Add(entryType, value, values, entryField, 0, valueOffset, className: className);

                if (entryType == PlanOutputLinkType.Value)
                {
                    AddValueReaders(value, 0);
                }
                else if (ReadReader(value) is { } reader)
                {
                    AddRow(value, reader, 0);
                }
                else if (className == PlanOutputValueLayout.ExpressionValueClass)
                {
                    AddExpressionExecutor(value, 0);
                }
                else if (TryRead(value, Layout.ExecValue.EntryField, out ulong entry) && entry >= TraceAddress.MinimumAddress)
                {
                    Add(PlanOutputLinkType.BufferEntry, entry, value, Layout.ExecValue.EntryField, 0);
                }
            }
        }

        private void AddRowBufferStorage(ulong iterator, MemoryField field, List<RowColumnBinding>? bound)
        {
            if (!TryRead(iterator, field, out ulong rowBuffer) || rowBuffer < TraceAddress.MinimumAddress)
            {
                return;
            }

            var count = bound is { Count: > 0 }
                ? bound.Max(b => b.StatusOffset) / (int)BufferEntrySize + 1
                : Math.Min(getSize(rowBuffer) / (int)BufferEntrySize, MaximumOutputs);

            if (count == 0)
            {
                return;
            }

            Add(PlanOutputLinkType.RowBuffer, rowBuffer, iterator, field, 0);

            for (var index = 0; index < count; index++)
            {
                var entryOffset = (ulong)index * BufferEntrySize;

                Add(PlanOutputLinkType.BufferEntry, rowBuffer + entryOffset, rowBuffer, Layout.RowBuffer.EntryField, 0, entryOffset);
            }
        }

        private (ulong Bindings, ulong Columns, List<RowColumnBinding> Bound)? ReadRowBindings(ulong iterator)
            => AccessorBindingLayout.ReadScan(memory, iterator) is { } scan
               && AccessorBindingLayout.ReadHeader(memory, scan.Bindings) is { } header
                ? (scan.Bindings, header.Columns, AccessorBindingLayout.ReadColumns(memory, header.Columns, header.ColumnCount))
                : null;

        private void AddRowBindingStorage(ulong iterator, (ulong Bindings, ulong Columns, List<RowColumnBinding> Bound) bindings)
        {
            Add(PlanOutputLinkType.RowBindings, bindings.Bindings, iterator, Layout.RowsetIterator.RowBindingsField, 0);

            Add(PlanOutputLinkType.ColumnBindings, bindings.Columns, bindings.Bindings, Layout.RowBindings.ColumnBindingsField, 0);

            foreach (var binding in bindings.Bound)
            {
                var bindingOffset = (ulong)(binding.Index * AccessorBindingLayout.ColumnSize);

                Add(PlanOutputLinkType.ColumnBinding,
                    bindings.Columns + bindingOffset,
                    bindings.Columns,
                    Layout.RowBindings.ColumnBindingField,
                    0,
                    bindingOffset,
                    binding.StatusOffset / (int)BufferEntrySize);

                Links[^1] = Links[^1] with { ColumnType = new SqlTypeInfo(binding.Type, 0, (ushort)binding.MaximumLength, 0, 0, 0) };
            }
        }

        private void AddAggregateStorage(ulong iterator)
        {
            var tableField = Layout.StreamAggregateIterator.VariableTableField;

            var rowSlot = (ulong)EngineLayout.StreamAggregateIteratorLayout.GroupRowSlot * PointerSize;

            if (TryRead(iterator, tableField, out ulong table)
                && table >= TraceAddress.MinimumAddress
                && rowSlot + PointerSize <= (ulong)getSize(table)
                && memory.TryRead(table + rowSlot, out ulong row)
                && row >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.VariableTable, table, iterator, tableField, 0);

                Add(PlanOutputLinkType.Variables, row, table, Layout.StreamAggregateIterator.VariablesField, 0, rowSlot);

                var count = Math.Min(getSize(row) / (int)BufferEntrySize, MaximumOutputs);

                for (var index = 0; index < count; index++)
                {
                    var entryOffset = (ulong)index * BufferEntrySize;

                    Add(PlanOutputLinkType.BufferEntry, row + entryOffset, row, Layout.RowBuffer.EntryField, 0, entryOffset);
                }
            }

            var countField = Layout.StreamAggregateIterator.CountField;

            if (countField.Offset + countField.Length <= getSize(iterator))
            {
                Add(PlanOutputLinkType.BufferEntry, GetFieldAddress(iterator, countField), iterator, countField, 0);
            }
        }

        private (int Slot, int? Offset, ulong Accessor, MemoryField SlotField)? AddReader(ulong address,
                                                                                         ulong source,
                                                                                         MemoryField sourceField,
                                                                                         int depth,
                                                                                         List<ulong> readers)
        {
            if (getClassName(address) == PlanOutputValueLayout.CachedSwitchClass)
            {
                if (!TryRead(address, Layout.CachedSwitch.ValueField, out ulong inner)
                    || getClassName(inner) == PlanOutputValueLayout.CachedSwitchClass)
                {
                    return null;
                }

                Add(PlanOutputLinkType.Reader, address, source, sourceField, depth, className: PlanOutputValueLayout.CachedSwitchClass);

                readers.Add(address);

                return AddReader(inner, address, Layout.CachedSwitch.ValueField, depth, readers);
            }

            if (getClassName(address) == PlanOutputValueLayout.ExpressionValueClass)
            {
                AddExpressionValue(address, source, sourceField, depth);

                return null;
            }

            if (ReadReader(address) is not { } reader)
            {
                return null;
            }

            Add(PlanOutputLinkType.Reader,
                address,
                source,
                sourceField,
                depth,
                slot: reader.Slot,
                offset: reader.Offset,
                className: getClassName(address));

            readers.Add(address);

            return reader;
        }

        private void AddExpressionValue(ulong expression, ulong source, MemoryField sourceField, int depth)
        {
            var record = Stage.Record;

            Add(PlanOutputLinkType.Expression,
                expression,
                source,
                sourceField,
                depth,
                className: PlanOutputValueLayout.ExpressionValueClass);

            AddExpressionExecutor(expression, depth);

            Stage.Record = record;

            SetStage(PlanOutputStageType.Expression, expression);

            if (getClassName(source) == PlanOutputValueLayout.CachedSwitchClass)
            {
                SetValue(new PlanOutputValueSource(PlanOutputLinkType.Expression,
                                                   source + (ulong)Layout.CachedSwitch.EntryOffset,
                                                   [],
                                                   null));
            }
        }

        private void AddExpressionExecutor(ulong expression, int depth)
        {
            var executorField = Layout.ExpressionValue.ExecutorField;

            if (TryRead(expression, executorField, out ulong executor) && executor >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.Executor, executor, expression, executorField, depth, className: getClassName(executor));

                AddExecutorInputs(executor, depth);
            }
        }

        private void AddExecutorInputs(ulong executor, int depth)
        {
            var valuesField = Layout.Executor.ValuesField;

            if (!TryRead(executor, valuesField, out ulong values) || values < TraceAddress.MinimumAddress)
            {
                return;
            }

            Add(PlanOutputLinkType.InputValues, values, executor, valuesField, depth);

            var count = Math.Min(getSize(values) / (int)PointerSize, MaximumEntries);

            var valueField = Layout.Executor.ValueField;

            for (var index = 0; index < count; index++)
            {
                var valueOffset = (ulong)index * PointerSize;

                if (!TryRead(values + valueOffset, valueField, out ulong value) || value < TraceAddress.MinimumAddress)
                {
                    continue;
                }

                var className = getClassName(value);

                Add(PlanOutputLinkType.InputValue, value, values, valueField, depth, valueOffset, className: className);

                if (ReadReader(value) is { } reader)
                {
                    AddRow(value, reader, depth);
                }
                else if (className == PlanOutputValueLayout.ExecValueClass
                         && TryRead(value, Layout.ExecValue.EntryField, out ulong entry)
                         && entry >= TraceAddress.MinimumAddress)
                {
                    Add(PlanOutputLinkType.BufferEntry, entry, value, Layout.ExecValue.EntryField, depth);
                }
            }
        }

        private void AddExecutor(ulong holder, MemoryField field)
        {
            if (TryRead(holder, field, out ulong executor)
                && executor >= TraceAddress.MinimumAddress
                && getClassName(executor) == PlanOutputValueLayout.ExecutorClass)
            {
                Add(PlanOutputLinkType.Executor, executor, holder, field, 0, className: PlanOutputValueLayout.ExecutorClass);

                AddExecutorInputs(executor, 0);
            }
        }

        private void AddExecutorArray(ulong holder, MemoryField field)
        {
            if (!TryRead(holder, field, out ulong array) || array < TraceAddress.MinimumAddress)
            {
                return;
            }

            Add(PlanOutputLinkType.Executors, array, holder, field, 0);

            var count = Math.Min(getSize(array) / (int)PointerSize, MaximumEntries);

            var element = Layout.Executor.ElementField;

            for (var index = 0; index < count; index++)
            {
                var offset = (ulong)index * PointerSize;

                if (TryRead(array + offset, element, out ulong executor)
                    && executor >= TraceAddress.MinimumAddress
                    && getClassName(executor) == PlanOutputValueLayout.ExecutorClass)
                {
                    Add(PlanOutputLinkType.Executor, executor, array, element, 0, offset, className: PlanOutputValueLayout.ExecutorClass);

                    AddExecutorInputs(executor, 0);
                }
            }
        }

        private (int Slot, int? Offset, ulong Accessor, MemoryField SlotField)? ReadReader(ulong address)
        {
            switch (getClassName(address))
            {
                case TraceHashTableLayout.ValueClass:
                    return (ReadSlot(address, Layout.HashValue.SlotField),
                            TryRead(address, Layout.HashValue.RecordOffsetField, out int offset) ? offset : (int?)null,
                            0,
                            Layout.HashValue.SlotField);
                case PlanOutputValueLayout.StorageValueClass:
                    TryRead(address, Layout.StorageValue.AccessorField, out ulong accessor);

                    return (ReadSlot(address, Layout.StorageValue.SlotField), null, accessor, Layout.StorageValue.SlotField);
                default:
                    return null;
            }
        }

        private int ReadSlot(ulong holder, MemoryField field) => TryRead(holder, field, out int slot) ? slot : -1;

        private void AddRow(ulong reader, (int Slot, int? Offset, ulong Accessor, MemoryField SlotField) info, int depth)
        {
            if (info.Slot < 0 || GetRowContext(depth) is not { } rowContext)
            {
                return;
            }

            var slotOffset = (ulong)info.Slot * PointerSize;

            var slotField = Layout.RowContext.SlotField;

            var rowSlot = GetFieldAddress(rowContext, slotField, slotOffset);

            Add(PlanOutputLinkType.RowContext, rowContext, reader, info.SlotField, depth, slot: info.Slot);

            Add(PlanOutputLinkType.RowSlot, rowSlot, rowContext, slotField, depth, slotOffset, info.Slot);

            SetStage(PlanOutputStageType.RowContext, rowContext);

            if (info.Offset is { } rowOffset)
            {
                SetValue(new PlanOutputValueSource(PlanOutputLinkType.RowSlot, rowSlot, [0], rowOffset));

                if (memory.TryRead(rowSlot, out ulong row) && row >= TraceAddress.MinimumAddress)
                {
                    Add(PlanOutputLinkType.Record, row, rowSlot, null, depth);
                }
            }

            if (info.Accessor == 0 || Stage.Record is not null || GetColumnOffset(info.Accessor) is not { } recordOffset)
            {
                return;
            }

            ulong[] chain = [0, 0, (ulong)Layout.Row.RecordField.Offset];

            Stage.Record = new PlanOutputValueSource(PlanOutputLinkType.Record, rowSlot, chain, recordOffset);

            if (memory.TryRead(rowSlot, out ulong rowset) && rowset >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.Rowset, rowset, rowSlot, null, depth);

                AddRecord(rowset, null, depth);
            }
        }

        private ulong? GetRowContext(int depth)
        {
            if (Links.FindIndex(l => l is { Type: PlanOutputLinkType.RowContext, IsRoute: true }) is >= 0 and var index)
            {
                return Links[index].Address;
            }

            if (GetAddress(PlanOutputLinkType.QueryContext) is null)
            {
                foreach (var iterator in Links.Where(l => l.Type == PlanOutputLinkType.Iterator).Select(l => l.Address).Distinct().ToList())
                {
                    if (AddContext(iterator))
                    {
                        break;
                    }
                }
            }

            if (GetAddress(PlanOutputLinkType.QueryContext) is not { } queryContext
                || !TryRead(queryContext, Layout.QueryContext.QueryScanField, out ulong queryScan)
                || !TryRead(queryScan, Layout.QueryScan.ContextField, out ulong scanContext)
                || !TryRead(scanContext, Layout.QueryScanContext.StatementField, out ulong statement)
                || !TryRead(statement, Layout.Statement.RowContextField, out ulong rowContext)
                || rowContext < TraceAddress.MinimumAddress)
            {
                return null;
            }

            Add(PlanOutputLinkType.QueryScan, queryScan, queryContext, Layout.QueryContext.QueryScanField, depth, isRoute: true);

            Add(PlanOutputLinkType.QueryScanContext, scanContext, queryScan, Layout.QueryScan.ContextField, depth, isRoute: true);

            Add(PlanOutputLinkType.Statement, statement, scanContext, Layout.QueryScanContext.StatementField, depth, isRoute: true);

            Add(PlanOutputLinkType.RowContext, rowContext, statement, Layout.Statement.RowContextField, depth, isRoute: true);

            return rowContext;
        }

        private ulong? AddBufferEntry(ulong reference, ulong iterator, int depth)
        {
            var bufferField = Layout.RowsetIterator.RowBufferField;

            if (!TryRead(reference, Layout.ColumnReference.OrdinalColumnField, out int column)
                || column is < 0 or >= MaximumOutputs
                || !TryRead(iterator, bufferField, out ulong rowBuffer)
                || rowBuffer < TraceAddress.MinimumAddress)
            {
                return null;
            }

            var entryOffset = (ulong)column * BufferEntrySize;

            if (entryOffset + BufferEntrySize > (ulong)getSize(rowBuffer))
            {
                return null;
            }

            var entry = rowBuffer + entryOffset;

            Add(PlanOutputLinkType.RowBuffer, rowBuffer, iterator, bufferField, depth);

            Add(PlanOutputLinkType.BufferEntry, entry, rowBuffer, Layout.RowBuffer.EntryField, depth, entryOffset);

            SetStage(PlanOutputStageType.RowBuffer, rowBuffer);

            SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, entry, [], null));

            return entry;
        }

        private ulong? AddAggregateValue(ulong reference, ulong producer, ulong iterator, int depth)
        {
            var groupField = Layout.PlanObject.GroupColumnsField;

            var aggregateField = Layout.PlanObject.AggregateColumnsField;

            var groupCountField = Layout.PlanObject.GroupCountField;

            if (FindColumn(producer, groupField, groupCountField, reference) is { } group)
            {
                return AddVariable(PlanOutputLinkType.GroupColumns, groupField, reference, producer, iterator, group, group.Index, depth);
            }

            if (FindColumn(producer, aggregateField, Layout.PlanObject.AggregateCountField, reference) is { } aggregate)
            {
                var variable = GetColumnCount(producer, groupField, groupCountField) + aggregate.Index;

                return AddVariable(PlanOutputLinkType.AggregateColumns,
                                   aggregateField,
                                   reference,
                                   producer,
                                   iterator,
                                   aggregate,
                                   variable,
                                   depth);
            }

            var countField = Layout.PlanObject.CountColumnsField;

            var valueField = Layout.StreamAggregateIterator.CountField;

            if (FindColumn(producer, countField, Layout.PlanObject.CountColumnCountField, reference) is not { Index: 0 } count
                || (ulong)(valueField.Offset + valueField.Length) > (ulong)getSize(iterator))
            {
                return null;
            }

            AddColumnArray(PlanOutputLinkType.CountColumns, countField, reference, producer, iterator, count, depth);

            var entry = GetFieldAddress(iterator, valueField);

            Add(PlanOutputLinkType.BufferEntry, entry, iterator, valueField, depth);

            SetStage(PlanOutputStageType.Counter, iterator);

            SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, entry, [], null));

            return entry;
        }

        private ulong? AddVariable(PlanOutputLinkType type,
                                   MemoryField field,
                                   ulong reference,
                                   ulong producer,
                                   ulong iterator,
                                   (ulong Array, int Index) column,
                                   int variable,
                                   int depth)
        {
            var tableField = Layout.StreamAggregateIterator.VariableTableField;

            var rowSlot = (ulong)EngineLayout.StreamAggregateIteratorLayout.GroupRowSlot * PointerSize;

            var entryOffset = (ulong)variable * BufferEntrySize;

            if (!TryRead(iterator, tableField, out ulong table)
                || table < TraceAddress.MinimumAddress
                || rowSlot + PointerSize > (ulong)getSize(table)
                || !memory.TryRead(table + rowSlot, out ulong row)
                || row < TraceAddress.MinimumAddress
                || entryOffset + BufferEntrySize > (ulong)getSize(row))
            {
                return null;
            }

            AddColumnArray(type, field, reference, producer, iterator, column, depth);

            Add(PlanOutputLinkType.VariableTable, table, iterator, tableField, depth);

            Add(PlanOutputLinkType.Variables, row, table, Layout.StreamAggregateIterator.VariablesField, depth, rowSlot);

            var entry = row + entryOffset;

            Add(PlanOutputLinkType.BufferEntry, entry, row, Layout.RowBuffer.EntryField, depth, entryOffset);

            SetStage(PlanOutputStageType.Variables, row);

            SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, entry, [], null));

            return entry;
        }

        private void AddColumnArray(PlanOutputLinkType type,
                                    MemoryField field,
                                    ulong reference,
                                    ulong producer,
                                    ulong iterator,
                                    (ulong Array, int Index) column,
                                    int depth)
        {
            Add(PlanOutputLinkType.PlanObject, producer, iterator, Layout.Iterator.PlanObjectField, depth, isRoute: true);

            Add(type, column.Array, producer, field, depth, isRoute: true);

            Add(PlanOutputLinkType.Reference, reference, column.Array, null, depth, (ulong)column.Index * PointerSize, isRoute: true);
        }

        private int GetColumnCount(ulong producer, MemoryField field, MemoryField countField)
            => TryRead(producer, field, out ulong array)
               && array >= TraceAddress.MinimumAddress
               && TryRead(producer, countField, out int count)
                ? Math.Clamp(count, 0, Math.Min(getSize(array) / (int)PointerSize, MaximumOutputs))
                : 0;

        private (ulong Array, int Index)? FindColumn(ulong producer, MemoryField field, MemoryField countField, ulong reference)
        {
            var count = GetColumnCount(producer, field, countField);

            var array = Read(producer, field);

            for (var index = 0; index < count; index++)
            {
                if (memory.TryRead(array + (ulong)index * PointerSize, out ulong column) && column == reference)
                {
                    return (array, index);
                }
            }

            return null;
        }

        private void AddInputValue(ulong iterator, ulong? entry, List<ulong> readers, int depth)
        {
            foreach (var valuesField in InputValueFields)
            {
                if (!TryRead(iterator, valuesField, out ulong values) || values < TraceAddress.MinimumAddress)
                {
                    continue;
                }

                var count = Math.Min(getSize(values) / (int)PointerSize, MaximumEntries);

                for (var index = 0; index < count; index++)
                {
                    var valueOffset = (ulong)index * PointerSize;

                    if (!TryRead(values + valueOffset, Layout.HashIterator.ValueField, out ulong value))
                    {
                        continue;
                    }

                    var isEntry = entry is { } target
                                  && TryRead(value, Layout.ExecValue.EntryField, out ulong pointer)
                                  && pointer == target;

                    if (!isEntry && !readers.Contains(value))
                    {
                        continue;
                    }

                    Add(PlanOutputLinkType.InputValues, values, iterator, valuesField, depth);

                    Add(PlanOutputLinkType.InputValue, value, values, Layout.HashIterator.ValueField, depth, valueOffset);

                    if (isEntry && entry is { } entryAddress)
                    {
                        Add(PlanOutputLinkType.BufferEntry, entryAddress, value, Layout.ExecValue.EntryField, depth);
                    }

                    return;
                }
            }
        }

        private int? GetColumnOffset(ulong accessor)
            => AccessorBindingLayout.ReadHeader(memory, accessor) is { ColumnCount: 1 } header
               && AccessorBindingLayout.ReadColumns(memory, header.Columns, 1) is [var binding]
                ? binding.RecordOffset
                : null;

        private bool TryRead<T>(ulong holder, MemoryField field, out T value) where T : unmanaged
            => memory.TryRead(GetFieldAddress(holder, field), out value);

        private ulong Read(ulong holder, MemoryField field) => TryRead(holder, field, out ulong value) ? value : 0;

        private void SetStage(PlanOutputStageType type, ulong address)
        {
            Stage.Type = type;

            Stage.Address = address;
        }

        private void SetValue(PlanOutputValueSource source)
        {
            var isEntry = source.Type == PlanOutputLinkType.BufferEntry;

            if (Stage.Value is not { Type: PlanOutputLinkType.BufferEntry } && (Stage.Value is null || isEntry))
            {
                Stage.Value = source;
            }
        }

        private ulong? GetAddress(PlanOutputLinkType type)
            => Links.FindIndex(l => l.Type == type) is >= 0 and var index ? Links[index].Address : null;

        private void Add(PlanOutputLinkType type,
                         ulong address,
                         ulong source,
                         MemoryField? field,
                         int depth,
                         ulong elementOffset = 0,
                         int slot = -1,
                         int? offset = null,
                         bool isRoute = false,
                         string? className = null)
            => Links.Add(new PlanOutputLink(type, address, position)
            {
                ClassName = className,
                Source = source,
                SourceOffset = (field?.Offset ?? 0) + (int)elementOffset,
                Field = field,
                Depth = depth,
                Slot = slot,
                Offset = offset,
                IsRoute = isRoute
            });
    }
}
