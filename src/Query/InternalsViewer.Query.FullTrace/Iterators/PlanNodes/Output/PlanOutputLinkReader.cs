using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.Interfaces;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

internal sealed class PlanOutputLinkReader(TraceResult trace)
{
    private const string GetRowMethod = "GetRow";

    private const int InstanceArgument = 0;

    private ConcurrentDictionary<(int NodeId, ulong Reference), PlanOutputPath> Cache { get; } = new();

    private Dictionary<ulong, TraceArgumentCall> FirstGetRows => field ??= GetFirstGetRows();

    private ulong[] PlanIterators => field ??= [.. trace.LinkedPlanNodes.Select(p => p.Iterator)];

    public PlanOutputPath Read(int nodeId, PlanOutputColumn column)
        => Cache.GetOrAdd((nodeId, column.Reference), _ => Find(nodeId, column));

    public IReadOnlyList<PlanOutputLink> Read(int nodeId, PlanOutputColumn column, MemoryHistoryReader memory)
    {
        var path = Read(nodeId, column);

        return path.Tails.Count > 0 ? Evaluate(path, CreateRoute(memory, memory.Position)) : path.Links;
    }

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

    public string? GetUnexpectedWriter(PlanOutputLink link)
        => link.Field is { Writers.Count: > 0 } field && GetWriter(link, field) is { } writer && !field.IsWrittenBy(writer)
            ? writer
            : null;

    internal static PlanOutputPath GetPath(IMemoryReader memory,
                                           ulong planObject,
                                           ulong iterator,
                                           ulong reference,
                                           TracePosition position,
                                           Func<ulong, string?> getClassName,
                                           Func<ulong, int> getSize,
                                           Func<ulong, bool> isRecord,
                                           IReadOnlyList<ulong> planIterators)
    {
        var route = new Route(memory, position, getClassName, getSize, isRecord, planIterators);

        route.AddOutput(planObject, iterator, reference);

        return route.GetPath();
    }

    internal static List<PlanOutputLink> GetLinks(PlanOutputPath path,
                                                  IMemoryReader memory,
                                                  TracePosition position,
                                                  Func<ulong, bool> isRecord)
        => Evaluate(path, new Route(memory, position, _ => null, _ => 0, isRecord, []));

    private static List<PlanOutputLink> Evaluate(PlanOutputPath path, Route route)
    {
        var next = 0;

        foreach (var (start, count, add) in path.Tails)
        {
            for (var index = next; index < start; index++)
            {
                route.Links.Add(path.Links[index]);
            }

            add(route);

            next = start + count;
        }

        for (var index = next; index < path.Links.Count; index++)
        {
            route.Links.Add(path.Links[index]);
        }

        return route.Links;
    }

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

        return route.GetPath();
    }

    private Route CreateRoute(IMemoryReader memory, TracePosition position)
        => new(memory,
               position,
               a => GetClassName(a, position),
               a => GetSize(a, position),
               a => trace.BufferPool.GetFrameReference(a, position)?.Offset >= PageHeader.Size,
               PlanIterators);

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

    internal sealed class Route(IMemoryReader memory,
                                TracePosition position,
                                Func<ulong, string?> getClassName,
                                Func<ulong, int> getSize,
                                Func<ulong, bool> isRecord,
                                IReadOnlyList<ulong> planIterators)
    {
        private const ulong PointerSize = sizeof(ulong);

        private const int MaximumEntries = 64;

        private const int MaximumOutputs = 4096;

        private const int MaximumLevels = 8;

        private const ulong WrappedSearchLength = 0x100;

        private const string JoinReferenceClass = "CValRefJoin";

        private const string OrdinalReferenceClass = "CValRefOrdinal";

        private const string StorageReferenceClass = "CValRefSS";

        private const string CalculatedReferenceClass = "CValRefCalculated";

        private const string ReferenceClassPrefix = "CValRef";

        private const string BatchIteratorPrefix = "CBpQScan";

        private const string BatchRedirectClass = "CValRefBatchRedirect";

        private const string AdapterClass = "CQScanBatchHelper";

        private const string CachedSwitchClass = "CValHashCachedSwitch";

        private const string StorageValueClass = "CValSSTable";

        private static readonly MemoryField[] OutputValueFields =
        [
            EngineLayout.HashIterator.BuildOutputValuesField,
            EngineLayout.HashIterator.ProbeOutputValuesField
        ];

        private static readonly MemoryField[] InputValueFields =
        [
            EngineLayout.HashIterator.BuildInputValuesField,
            EngineLayout.HashIterator.ProbeInputValuesField
        ];

        public List<PlanOutputLink> Links { get; } = [];

        public List<(int Start, int Count, Action<Route> Add)> Tails { get; } = [];

        private static ulong BufferEntrySize { get; } = (ulong)EngineLayout.RowBuffer.EntryField.Length;

        private PlanOutputValueSource? Value { get; set; }

        private PlanOutputValueSource? Record { get; set; }

        public void AddOutput(ulong planObject, ulong iterator, ulong reference)
        {
            if (!TryRead(planObject, EngineLayout.PlanObject.OutputColumnsField, out ulong outputs)
                || !TryRead(outputs, EngineLayout.OutputList.CountField, out int count)
                || !TryRead(outputs, EngineLayout.OutputList.ColumnsField, out ulong array))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputList, outputs, planObject, EngineLayout.PlanObject.OutputColumnsField, 0, isRoute: true);

            Add(PlanOutputLinkType.OutputArray, array, outputs, EngineLayout.OutputList.ColumnsField, 0, isRoute: true);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumOutputs); index++)
            {
                var columnOffset = index * PointerSize;

                var columnField = EngineLayout.OutputList.ColumnField;

                if (TryRead(array + columnOffset, columnField, out ulong found) && found == reference)
                {
                    Add(PlanOutputLinkType.Reference, reference, array, columnField, 0, columnOffset, isRoute: true);

                    var owner = AddMap(iterator) && GetIterator(planObject, 0) is { } ownIterator ? ownIterator : iterator;

                    AddReference(reference, owner, 0);

                    return;
                }
            }
        }

        public PlanOutputPath GetPath() => new() { Links = Links, Tails = Tails, Value = Value, Record = Record };

        private static ulong GetFieldAddress(ulong holder, MemoryField field, ulong elementOffset = 0)
            => holder + (ulong)field.Offset + elementOffset;

        private ulong? AddReference(ulong reference, ulong owner, int depth)
        {
            var className = getClassName(reference);

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
                ? EngineLayout.ColumnReference.StorageProducerField
                : EngineLayout.ColumnReference.ProducerField;

            if (!TryRead(reference, producerField, out ulong producer) || getClassName(producer) is null)
            {
                return null;
            }

            Add(PlanOutputLinkType.Producer, producer, reference, producerField, depth, isRoute: true);

            if (GetIterator(producer, depth) is not { } producerIterator || IsBatch(owner) || IsBatch(producerIterator))
            {
                return null;
            }

            if (producerIterator != owner)
            {
                AddInputPath(owner, producerIterator, depth);
            }

            if (className == JoinReferenceClass)
            {
                AddJoinValue(reference, producerIterator, depth);

                return null;
            }

            if (className == StorageReferenceClass)
            {
                AddTail(r => r.AddStorageRow(producerIterator, depth));

                return null;
            }

            return className == OrdinalReferenceClass ? AddBufferEntry(reference, producerIterator, depth) : null;
        }

        private void AddExpression(ulong reference, ulong owner, int depth)
        {
            var producerField = EngineLayout.ColumnReference.CalculatedProducerField;

            if (TryRead(reference, producerField, out ulong producer) && getClassName(producer) is not null)
            {
                Add(PlanOutputLinkType.Producer, producer, reference, producerField, depth, isRoute: true);
            }

            if (!TryRead(reference, EngineLayout.ColumnReference.ExpressionField, out ulong expression)
                || !TryRead(expression, EngineLayout.Expression.OperandsField, out ulong operands)
                || !TryRead(expression, EngineLayout.Expression.OperandCountField, out int count))
            {
                return;
            }

            Add(PlanOutputLinkType.Expression, expression, reference, EngineLayout.ColumnReference.ExpressionField, depth, isRoute: true);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumEntries); index++)
            {
                var operandOffset = index * PointerSize;

                if (!TryRead(operands + operandOffset, EngineLayout.Expression.OperandField, out ulong operand))
                {
                    continue;
                }

                var interfaceStart = operand - EngineLayout.ColumnReference.InterfaceOffset;

                var input = IsReference(interfaceStart) ? interfaceStart : operand;

                if (!IsReference(input))
                {
                    continue;
                }

                Add(PlanOutputLinkType.Input, input, operands, EngineLayout.Expression.OperandField, depth, operandOffset, isRoute: true);

                AddReference(input, owner, depth + 1);
            }
        }

        private void AddBatchColumn(ulong reference, int depth)
        {
            var producerField = EngineLayout.ColumnReference.RedirectProducerField;

            if (!TryRead(reference, producerField, out ulong planObject)
                || !TryRead(reference, EngineLayout.ColumnReference.RedirectColumnField, out int column)
                || column is < 0 or >= MaximumOutputs)
            {
                return;
            }

            Add(PlanOutputLinkType.Producer, planObject, reference, producerField, depth, isRoute: true);

            if (FindAdapter(planObject) is not { } adapter)
            {
                return;
            }

            if (TryRead(adapter, EngineLayout.BatchAdapter.ChildField, out ulong child) && child >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.Iterator, child, adapter, EngineLayout.BatchAdapter.ChildField, depth);
            }

            var columnOffset = (ulong)column * PointerSize;

            if (TryRead(adapter, EngineLayout.BatchAdapter.OutputValuesField, out ulong values)
                && TryRead(values + columnOffset, EngineLayout.BatchAdapter.OutputValueField, out ulong value))
            {
                Add(PlanOutputLinkType.OutputValues, values, adapter, EngineLayout.BatchAdapter.OutputValuesField, depth);

                Add(PlanOutputLinkType.Value, value, values, EngineLayout.BatchAdapter.OutputValueField, depth, columnOffset);
            }

            var entryOffset = (ulong)column * BufferEntrySize;

            if (TryRead(adapter, EngineLayout.BatchAdapter.RowBufferField, out ulong rowBuffer) && rowBuffer >= TraceAddress.MinimumAddress)
            {
                Add(PlanOutputLinkType.RowBuffer, rowBuffer, adapter, EngineLayout.BatchAdapter.RowBufferField, depth);

                var entryField = EngineLayout.RowBuffer.EntryField;

                Add(PlanOutputLinkType.BufferEntry, rowBuffer + entryOffset, rowBuffer, entryField, depth, entryOffset);

                SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, rowBuffer + entryOffset, [], null), depth);
            }

            AddTail(r => r.AddBatchVector(adapter, column, depth));
        }

        private void AddBatchVector(ulong adapter, int column, int depth)
        {
            var vectorField = EngineLayout.BatchAdapter.ColumnMapVectorField;

            var mapOffset = (ulong)(column * vectorField.Length);

            if (!TryRead(adapter, EngineLayout.BatchAdapter.CursorField, out int cursor)
                || !TryRead(adapter, EngineLayout.BatchAdapter.ColumnMapField, out ulong map)
                || !TryRead(map + mapOffset, vectorField, out ushort vectorId)
                || !TryRead(adapter, EngineLayout.BatchAdapter.BatchField, out ulong batch)
                || batch < TraceAddress.MinimumAddress)
            {
                return;
            }

            var cursorField = EngineLayout.BatchAdapter.CursorField;

            Add(PlanOutputLinkType.Cursor, GetFieldAddress(adapter, cursorField), adapter, cursorField, depth, slot: cursor);

            Add(PlanOutputLinkType.ColumnMap, map, adapter, EngineLayout.BatchAdapter.ColumnMapField, depth);

            Add(PlanOutputLinkType.VectorId, GetFieldAddress(map, vectorField, mapOffset), map, vectorField, depth, mapOffset, vectorId);

            Add(PlanOutputLinkType.Batch, batch, adapter, EngineLayout.BatchAdapter.BatchField, depth);

            var rowField = EngineLayout.Batch.SelectedRowField;

            var selectionOffset = (ulong)((cursor - 1) * rowField.Length);

            if (cursor < 1
                || vectorId < 1
                || !TryRead(batch, EngineLayout.Batch.SelectionField, out ulong selection)
                || !TryRead(selection + selectionOffset, rowField, out ushort row)
                || !TryRead(batch, EngineLayout.Batch.VectorsField, out ulong vectors))
            {
                return;
            }

            Add(PlanOutputLinkType.Selection, selection, batch, EngineLayout.Batch.SelectionField, depth);

            Add(PlanOutputLinkType.SelectedRow, selection + selectionOffset, selection, rowField, depth, selectionOffset, row);

            Add(PlanOutputLinkType.Vectors, vectors, batch, EngineLayout.Batch.VectorsField, depth);

            var vectorOffset = (ulong)((vectorId - 1) * BatchReader.VectorEntrySize);

            if (!TryRead(vectors + vectorOffset, EngineLayout.Batch.VectorField, out ulong buffer) || buffer < TraceAddress.MinimumAddress)
            {
                return;
            }

            var slotOffset = (ulong)row * PointerSize;

            Add(PlanOutputLinkType.Vector, buffer, vectors, EngineLayout.Batch.VectorField, depth, vectorOffset, vectorId);

            Add(PlanOutputLinkType.VectorSlot, buffer + slotOffset, buffer, EngineLayout.Batch.VectorSlotField, depth, slotOffset, row);
        }

        private ulong? FindAdapter(ulong planObject)
        {
            foreach (var iterator in planIterators)
            {
                if (getClassName(iterator) == AdapterClass && Read(iterator, EngineLayout.Iterator.PlanObjectField) == planObject)
                {
                    return iterator;
                }
            }

            return null;
        }

        private void AddStorageRow(ulong iterator, int depth)
        {
            if (!TryRead(iterator, EngineLayout.ScanIterator.RowsetField, out ulong rowset)
                || rowset < TraceAddress.MinimumAddress + EngineLayout.Rowset.InterfaceOffset)
            {
                return;
            }

            Add(PlanOutputLinkType.Rowset, rowset, iterator, EngineLayout.ScanIterator.RowsetField, depth);

            var rowsetStart = rowset - EngineLayout.Rowset.InterfaceOffset;

            if (!TryRead(rowsetStart, EngineLayout.Rowset.RowField, out ulong row) || row < TraceAddress.MinimumAddress)
            {
                return;
            }

            Add(PlanOutputLinkType.Row, row, rowsetStart, EngineLayout.Rowset.RowField, depth);

            var recordField = EngineLayout.Row.RecordField;

            if (TryRead(row, recordField, out ulong record) && isRecord(record))
            {
                Add(PlanOutputLinkType.Record, GetFieldAddress(row, recordField), row, recordField, depth);
            }
        }

        private bool IsBatch(ulong iterator) => getClassName(iterator)?.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal) == true;

        private bool IsReference(ulong address)
            => getClassName(address)?.StartsWith(ReferenceClassPrefix, StringComparison.Ordinal) == true;

        private bool AddMap(ulong iterator)
        {
            if (IsBatch(iterator)
                || !TryRead(iterator, EngineLayout.Iterator.ExecutionContextField, out ulong context)
                || !TryRead(context, EngineLayout.ExecutionContext.QueryContextField, out ulong queryContext)
                || !TryRead(queryContext, EngineLayout.QueryContext.IteratorMapField, out ulong map))
            {
                return false;
            }

            Add(PlanOutputLinkType.ExecutionContext, context, iterator, EngineLayout.Iterator.ExecutionContextField, 0, isRoute: true);

            Add(PlanOutputLinkType.QueryContext, queryContext, context, EngineLayout.ExecutionContext.QueryContextField, 0, isRoute: true);

            Add(PlanOutputLinkType.IteratorMap, map, queryContext, EngineLayout.QueryContext.IteratorMapField, 0, isRoute: true);

            return true;
        }

        private ulong? GetIterator(ulong planObject, int depth)
        {
            var map = GetAddress(PlanOutputLinkType.IteratorMap) ?? 0;

            var buckets = map == 0 ? 0 : Math.Min((ulong)getSize(map) / PointerSize, (ulong)MaximumEntries);

            for (var bucket = 0UL; bucket < buckets; bucket++)
            {
                var entries = new List<(ulong Entry, ulong Source, MemoryField Field, ulong Offset)>();

                var source = map;

                var field = EngineLayout.IteratorMap.BucketField;

                var offset = bucket * PointerSize;

                while (entries.Count < MaximumEntries && TryRead(source + offset, field, out ulong entry) && entry != 0)
                {
                    entries.Add((entry, source, field, offset));

                    if (TryRead(entry, EngineLayout.IteratorMap.PlanObjectField, out ulong found)
                        && found == planObject
                        && TryRead(entry, EngineLayout.IteratorMap.IteratorField, out ulong iterator))
                    {
                        foreach (var (mapEntry, mapSource, mapField, mapOffset) in entries)
                        {
                            Add(PlanOutputLinkType.MapEntry, mapEntry, mapSource, mapField, depth, mapOffset, isRoute: true);
                        }

                        Add(PlanOutputLinkType.Iterator, iterator, entry, EngineLayout.IteratorMap.IteratorField, depth, isRoute: true);

                        return iterator;
                    }

                    source = entry;

                    field = EngineLayout.IteratorMap.NextField;

                    offset = 0;
                }
            }

            foreach (var planIterator in planIterators)
            {
                if (Read(planIterator, EngineLayout.Iterator.PlanObjectField) != planObject)
                {
                    continue;
                }

                var (iterator, offset) = GetWrapped(planIterator) ?? (planIterator, 0UL);

                Add(PlanOutputLinkType.Iterator, iterator, planIterator, null, depth, offset, isRoute: true);

                return iterator;
            }

            return null;
        }

        private void AddInputPath(ulong owner, ulong target, int depth)
        {
            var path = new List<(PlanOutputLinkType Type, ulong Address, ulong Source, MemoryField? Field, ulong Offset)>();

            if (!FindInputPath(owner, target, path, 0))
            {
                return;
            }

            foreach (var (type, address, source, field, offset) in path)
            {
                Add(type, address, source, field, depth, offset);
            }
        }

        private bool FindInputPath(ulong iterator,
                                   ulong target,
                                   List<(PlanOutputLinkType Type, ulong Address, ulong Source, MemoryField? Field, ulong Offset)> path,
                                   int level)
        {
            if (level >= MaximumLevels
                || !TryRead(iterator, EngineLayout.Iterator.InputsField, out ulong inputs)
                || inputs < TraceAddress.MinimumAddress)
            {
                return false;
            }

            var size = getSize(inputs);

            var count = size > 0 ? Math.Min(size / (int)PointerSize, MaximumEntries) : MaximumEntries;

            for (var index = 0; index < count; index++)
            {
                var inputOffset = (ulong)index * PointerSize;

                if (!TryRead(inputs + inputOffset, EngineLayout.Iterator.InputField, out ulong input)
                    || input < TraceAddress.MinimumAddress)
                {
                    break;
                }

                var mark = path.Count;

                path.Add((PlanOutputLinkType.Inputs, inputs, iterator, EngineLayout.Iterator.InputsField, 0));

                var child = input;

                if (GetWrapped(input) is { } wrapped)
                {
                    path.Add((PlanOutputLinkType.ProfilingIterator, input, inputs, EngineLayout.Iterator.InputField, inputOffset));

                    path.Add((PlanOutputLinkType.Iterator, wrapped.Iterator, input, null, wrapped.Offset));

                    child = wrapped.Iterator;
                }
                else
                {
                    path.Add((PlanOutputLinkType.Iterator, input, inputs, EngineLayout.Iterator.InputField, inputOffset));
                }

                if (child == target || FindInputPath(child, target, path, level + 1))
                {
                    return true;
                }

                path.RemoveRange(mark, path.Count - mark);
            }

            return false;
        }

        private (ulong Iterator, ulong Offset)? GetWrapped(ulong profiling)
        {
            if (!TryRead(profiling, EngineLayout.Iterator.PlanObjectField, out ulong planObject) || planObject == 0)
            {
                return null;
            }

            for (var offset = 0UL; offset < WrappedSearchLength; offset += PointerSize)
            {
                if (memory.TryRead(profiling + offset, out ulong found)
                    && found != profiling
                    && found != planObject
                    && getSize(found) > 0
                    && Read(found, EngineLayout.Iterator.PlanObjectField) == planObject)
                {
                    return (found, offset);
                }
            }

            return null;
        }

        private void AddJoinValue(ulong reference, ulong iterator, int depth)
        {
            if (!TryRead(reference, EngineLayout.ColumnReference.JoinSideField, out ushort side)
                || side >= OutputValueFields.Length
                || !TryRead(reference, EngineLayout.ColumnReference.JoinColumnField, out int column)
                || column is < 0 or >= MaximumOutputs)
            {
                return;
            }

            var valuesField = OutputValueFields[side];

            var valueOffset = (ulong)column * PointerSize;

            if (!TryRead(iterator, valuesField, out ulong values)
                || !TryRead(values + valueOffset, EngineLayout.HashIterator.ValueField, out ulong value))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputValues, values, iterator, valuesField, depth);

            Add(PlanOutputLinkType.Value, value, values, EngineLayout.HashIterator.ValueField, depth, valueOffset);

            AddValue(value, iterator, depth);
        }

        private void AddValue(ulong value, ulong iterator, int depth)
        {
            if (ReadReader(value) is { } self)
            {
                Links[^1] = Links[^1] with { Slot = self.Slot, Offset = self.Offset };

                AddRow(value, self, depth);

                return;
            }

            var readers = new List<ulong>();

            var readerField = EngineLayout.SwitchValue.ReaderField;

            if (!TryRead(value, readerField, out ulong primary))
            {
                return;
            }

            if (AddReader(primary, value, readerField, depth, readers) is { } reader)
            {
                AddRow(readers[^1], reader, depth);
            }

            var inputField = EngineLayout.SwitchValue.InputField;

            if (!TryRead(value, inputField, out ulong input) || !IsReference(input))
            {
                return;
            }

            Add(PlanOutputLinkType.Input, input, value, inputField, depth, isRoute: true);

            var entry = AddReference(input, iterator, depth + 1);

            if (readers.Count == 1
                && entry is { } inputEntry
                && TryRead(primary, EngineLayout.CachedSwitch.ValueField, out ulong inner)
                && Read(inner, EngineLayout.ExecValue.EntryField) == inputEntry)
            {
                Add(PlanOutputLinkType.InputValue, inner, primary, EngineLayout.CachedSwitch.ValueField, depth);

                Add(PlanOutputLinkType.BufferEntry, inputEntry, inner, EngineLayout.ExecValue.EntryField, depth);

                SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, inputEntry, [], null), depth);
            }

            AddInputValue(iterator, entry, readers, depth + 1);
        }

        private (int Slot, int? Offset, ulong Accessor, MemoryField SlotField)? AddReader(ulong address,
                                                                                         ulong source,
                                                                                         MemoryField sourceField,
                                                                                         int depth,
                                                                                         List<ulong> readers)
        {
            if (getClassName(address) == CachedSwitchClass)
            {
                if (!TryRead(address, EngineLayout.CachedSwitch.ValueField, out ulong inner) || getClassName(inner) == CachedSwitchClass)
                {
                    return null;
                }

                Add(PlanOutputLinkType.Reader, address, source, sourceField, depth);

                readers.Add(address);

                return AddReader(inner, address, EngineLayout.CachedSwitch.ValueField, depth, readers);
            }

            if (ReadReader(address) is not { } reader)
            {
                return null;
            }

            Add(PlanOutputLinkType.Reader, address, source, sourceField, depth, slot: reader.Slot, offset: reader.Offset);

            readers.Add(address);

            return reader;
        }

        private (int Slot, int? Offset, ulong Accessor, MemoryField SlotField)? ReadReader(ulong address)
        {
            switch (getClassName(address))
            {
                case TraceHashTableLayout.ValueClass:
                    return (ReadSlot(address, EngineLayout.HashValue.SlotField),
                            TryRead(address, EngineLayout.HashValue.RecordOffsetField, out int offset) ? offset : (int?)null,
                            0,
                            EngineLayout.HashValue.SlotField);
                case StorageValueClass:
                    TryRead(address, EngineLayout.StorageValue.AccessorField, out ulong accessor);

                    return (ReadSlot(address, EngineLayout.StorageValue.SlotField), null, accessor, EngineLayout.StorageValue.SlotField);
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

            var slotField = EngineLayout.RowContext.SlotField;

            var rowSlot = GetFieldAddress(rowContext, slotField, slotOffset);

            Add(PlanOutputLinkType.RowContext, rowContext, reader, info.SlotField, depth, slot: info.Slot);

            Add(PlanOutputLinkType.RowSlot, rowSlot, rowContext, slotField, depth, slotOffset, info.Slot);

            if (info.Offset is { } rowOffset)
            {
                SetValue(new PlanOutputValueSource(PlanOutputLinkType.RowSlot, rowSlot, [0], rowOffset), depth);
            }

            if (info.Accessor == 0)
            {
                return;
            }

            var columnOffset = GetColumnOffset(info.Accessor);

            if (depth == 0 && Record is null && columnOffset is { } recordOffset)
            {
                ulong[] chain = [0, 0, (ulong)EngineLayout.Row.RecordField.Offset];

                Record = new PlanOutputValueSource(PlanOutputLinkType.Record, rowSlot, chain, recordOffset);
            }

            var slot = info.Slot;

            AddTail(r => r.AddRowRecord(rowContext, slotOffset, slot, columnOffset, depth));
        }

        private void AddRowRecord(ulong rowContext, ulong slotOffset, int slot, int? columnOffset, int depth)
        {
            var slotField = EngineLayout.RowContext.SlotField;

            var recordField = EngineLayout.Row.RecordField;

            if (!memory.TryRead(GetFieldAddress(rowContext, slotField, slotOffset), out ulong rowset)
                || !memory.TryRead(rowset, out ulong row)
                || !TryRead(row, recordField, out ulong record)
                || !isRecord(record))
            {
                return;
            }

            Add(PlanOutputLinkType.Rowset, rowset, rowContext, slotField, depth, slotOffset, slot);

            Add(PlanOutputLinkType.Row, row, rowset, null, depth);

            Add(PlanOutputLinkType.Record, GetFieldAddress(row, recordField), row, recordField, depth, slot: slot, offset: columnOffset);
        }

        private ulong? GetRowContext(int depth)
        {
            if (Links.FindIndex(l => l is { Type: PlanOutputLinkType.RowContext, IsRoute: true }) is >= 0 and var index)
            {
                return Links[index].Address;
            }

            if (GetAddress(PlanOutputLinkType.QueryContext) is not { } queryContext
                || !TryRead(queryContext, EngineLayout.QueryContext.QueryScanField, out ulong queryScan)
                || !TryRead(queryScan, EngineLayout.QueryScan.ContextField, out ulong scanContext)
                || !TryRead(scanContext, EngineLayout.QueryScanContext.StatementField, out ulong statement)
                || !TryRead(statement, EngineLayout.Statement.RowContextField, out ulong rowContext)
                || rowContext < TraceAddress.MinimumAddress)
            {
                return null;
            }

            Add(PlanOutputLinkType.QueryScan, queryScan, queryContext, EngineLayout.QueryContext.QueryScanField, depth, isRoute: true);

            Add(PlanOutputLinkType.QueryScanContext, scanContext, queryScan, EngineLayout.QueryScan.ContextField, depth, isRoute: true);

            Add(PlanOutputLinkType.Statement, statement, scanContext, EngineLayout.QueryScanContext.StatementField, depth, isRoute: true);

            Add(PlanOutputLinkType.RowContext, rowContext, statement, EngineLayout.Statement.RowContextField, depth, isRoute: true);

            return rowContext;
        }

        private ulong? AddBufferEntry(ulong reference, ulong iterator, int depth)
        {
            var bufferField = EngineLayout.ScanIterator.RowBufferField;

            if (!TryRead(reference, EngineLayout.ColumnReference.OrdinalColumnField, out int column)
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

            Add(PlanOutputLinkType.BufferEntry,
                entry,
                rowBuffer,
                EngineLayout.RowBuffer.EntryField,
                depth,
                entryOffset,
                offset: GetRecordOffset(iterator, (int)entryOffset));

            SetValue(new PlanOutputValueSource(PlanOutputLinkType.BufferEntry, entry, [], null), depth);

            return entry;
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

                    if (!TryRead(values + valueOffset, EngineLayout.HashIterator.ValueField, out ulong value))
                    {
                        continue;
                    }

                    var isEntry = entry is { } target
                                  && TryRead(value, EngineLayout.ExecValue.EntryField, out ulong pointer)
                                  && pointer == target;

                    if (!isEntry && !readers.Contains(value))
                    {
                        continue;
                    }

                    Add(PlanOutputLinkType.InputValues, values, iterator, valuesField, depth);

                    Add(PlanOutputLinkType.InputValue, value, values, EngineLayout.HashIterator.ValueField, depth, valueOffset);

                    if (isEntry && entry is { } entryAddress)
                    {
                        Add(PlanOutputLinkType.BufferEntry, entryAddress, value, EngineLayout.ExecValue.EntryField, depth);
                    }

                    return;
                }
            }
        }

        private int? GetRecordOffset(ulong iterator, int entryOffset)
        {
            if (AccessorBindingLayout.ReadScan(memory, iterator) is not { } scan
                || AccessorBindingLayout.ReadHeader(memory, scan.Bindings) is not { } header)
            {
                return null;
            }

            var bindings = AccessorBindingLayout.ReadColumns(memory, header.Columns, header.ColumnCount);

            return bindings.Find(b => b.StatusOffset == entryOffset) is { } binding ? binding.RecordOffset : null;
        }

        private int? GetColumnOffset(ulong accessor)
            => AccessorBindingLayout.ReadHeader(memory, accessor) is { ColumnCount: 1 } header
               && AccessorBindingLayout.ReadColumns(memory, header.Columns, 1) is [var binding]
                ? binding.RecordOffset
                : null;

        private bool TryRead<T>(ulong holder, MemoryField field, out T value) where T : unmanaged
            => memory.TryRead(GetFieldAddress(holder, field), out value);

        private ulong Read(ulong holder, MemoryField field) => TryRead(holder, field, out ulong value) ? value : 0;

        private void AddTail(Action<Route> add)
        {
            var start = Links.Count;

            add(this);

            Tails.Add((start, Links.Count - start, add));
        }

        private void SetValue(PlanOutputValueSource source, int depth)
        {
            var isEntry = source.Type == PlanOutputLinkType.BufferEntry;

            if (depth == 0 && Value is not { Type: PlanOutputLinkType.BufferEntry } && (Value is null || isEntry))
            {
                Value = source;
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
                         bool isRoute = false)
            => Links.Add(new PlanOutputLink(type, address, position)
            {
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
