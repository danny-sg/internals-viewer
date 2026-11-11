using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed class PlanOutputLinkReader(TraceResult trace)
{
    private const string GetRowMethod = "GetRow";

    private const int InstanceArgument = 0;

    private ConcurrentDictionary<(int NodeId, ulong Reference), List<PlanOutputLink>> Cache { get; } = new();

    private Dictionary<ulong, TraceArgumentCall> FirstGetRows => field ??= GetFirstGetRows();

    public List<PlanOutputLink> Read(int nodeId, PlanOutputColumn column)
        => Cache.GetOrAdd((nodeId, column.Reference), _ => ReadLinks(nodeId, column, null));

    public List<PlanOutputLink> Read(int nodeId, PlanOutputColumn column, TracePosition position)
        => ReadLinks(nodeId, column, position);

    internal static List<PlanOutputLink> GetLinks(IMemoryReader memory,
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

        return route.Links;
    }

    private List<PlanOutputLink> ReadLinks(int nodeId, PlanOutputColumn column, TracePosition? at)
    {
        var planNodes = trace.LinkedPlanNodes.Where(p => p.NodeId == nodeId).ToList();

        var call = planNodes.Concat(trace.LinkedPlanNodes.Where(p => p.NodeId == column.ProducerNodeId))
                            .Select(p => FirstGetRows.GetValueOrDefault(p.Iterator))
                            .FirstOrDefault(c => c is not null)
                   ?? FirstGetRows.Values.MaxBy(c => c.ToPosition);

        if (planNodes.Count == 0 || (at is null && call is null))
        {
            return [];
        }

        var position = at ?? (call!.ToPosition != TracePosition.Unknown ? call.ToPosition : call.FromPosition);

        var store = trace.MemoryHistory;

        return GetLinks(new MemoryHistoryReader(store, position),
                        planNodes[0].PlanObject,
                        planNodes[0].Iterator,
                        column.Reference,
                        position,
                        a => GetClassName(a, position),
                        a => store.GetBlock(a, position) is >= 0 and var block && store.Blocks[block].Address == a
                            ? store.Blocks[block].Size
                            : 0,
                        a => trace.BufferPool.GetFrameReference(a, position)?.Offset >= PageHeader.Size,
                        [.. trace.LinkedPlanNodes.Select(p => p.Iterator)]);
    }

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

    private sealed class Route(IMemoryReader memory,
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

        private const string CachedSwitchClass = "CValHashCachedSwitch";

        private const string StorageValueClass = "CValSSTable";

        private const ulong JoinSideOffset = 0x58;

        private const ulong JoinColumnOffset = 0x5C;

        private const ulong OrdinalColumnOffset = 0x58;

        private const ulong CalculatedProducerOffset = 0x58;

        private const ulong CalculatedExpressionOffset = 0x68;

        private const ulong ExpressionOperandsOffset = 0x28;

        private const ulong ExpressionOperandCountOffset = 0x30;

        private const ulong ReferenceInterfaceOffset = 0x28;

        private const ulong RowsetInterfaceOffset = 0x70;

        private const ulong RowsetRowOffset = 0xB8;

        private const ulong QueryContextOffset = 0x1B8;

        private const ulong IteratorMapOffset = 0x290;

        private const ulong MapEntryPlanObjectOffset = 0x08;

        private const ulong MapEntryIteratorOffset = 0x10;

        private const ulong QueryScanOffset = 0x20;

        private const ulong QueryScanContextOffset = 0x58;

        private const ulong StatementOffset = 0x28;

        private const ulong RowContextOffset = 0xD8;

        private const ulong BufferEntrySize = 0x20;

        private const ulong ValueEntryOffset = 0x08;

        private const ulong CachedSwitchValueOffset = 0x08;

        private const ulong StorageValueSlotOffset = 0x10;

        private const ulong StorageValueAccessorOffset = 0x18;

        private const ulong RowRecordOffset = 0x50;

        private static readonly PlanNodeLayout Layout = PlanNodeLayout.SqlServer2025;

        public List<PlanOutputLink> Links { get; } = [];

        public void AddOutput(ulong planObject, ulong iterator, ulong reference)
        {
            if (!memory.TryRead(planObject + Layout.PlanOutputsOffset, out ulong outputs)
                || !memory.TryRead(outputs + Layout.OutputCountOffset, out int count)
                || !memory.TryRead(outputs + Layout.OutputColumnsOffset, out ulong array))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputList, outputs, planObject, Layout.PlanOutputsOffset, 0, isRoute: true);

            Add(PlanOutputLinkType.OutputArray, array, outputs, Layout.OutputColumnsOffset, 0, isRoute: true);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumOutputs); index++)
            {
                if (memory.TryRead(array + index * PointerSize, out ulong found) && found == reference)
                {
                    Add(PlanOutputLinkType.Reference, reference, array, index * PointerSize, 0, isRoute: true);

                    var owner = AddMap(iterator) && GetIterator(planObject, 0) is { } ownIterator ? ownIterator : iterator;

                    AddReference(reference, owner, 0);

                    return;
                }
            }
        }

        private ulong? AddReference(ulong reference, ulong owner, int depth)
        {
            var className = getClassName(reference);

            if (className == CalculatedReferenceClass)
            {
                AddExpression(reference, owner, depth);

                return null;
            }

            var producerOffset = className == StorageReferenceClass ? Layout.StorageColumnProducerOffset : Layout.ColumnProducerOffset;

            if (!memory.TryRead(reference + producerOffset, out ulong producer) || getClassName(producer) is null)
            {
                return null;
            }

            Add(PlanOutputLinkType.Producer, producer, reference, producerOffset, depth, isRoute: true);

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
                AddStorageRow(producerIterator, depth);

                return null;
            }

            return className == OrdinalReferenceClass ? AddBufferEntry(reference, producerIterator, depth) : null;
        }

        private void AddExpression(ulong reference, ulong owner, int depth)
        {
            if (memory.TryRead(reference + CalculatedProducerOffset, out ulong producer) && getClassName(producer) is not null)
            {
                Add(PlanOutputLinkType.Producer, producer, reference, CalculatedProducerOffset, depth, isRoute: true);
            }

            if (!memory.TryRead(reference + CalculatedExpressionOffset, out ulong expression)
                || !memory.TryRead(expression + ExpressionOperandsOffset, out ulong operands)
                || !memory.TryRead(expression + ExpressionOperandCountOffset, out int count))
            {
                return;
            }

            Add(PlanOutputLinkType.Expression, expression, reference, CalculatedExpressionOffset, depth, isRoute: true);

            for (var index = 0UL; index < (ulong)Math.Clamp(count, 0, MaximumEntries); index++)
            {
                if (!memory.TryRead(operands + index * PointerSize, out ulong operand))
                {
                    continue;
                }

                var input = IsReference(operand - ReferenceInterfaceOffset) ? operand - ReferenceInterfaceOffset : operand;

                if (!IsReference(input))
                {
                    continue;
                }

                Add(PlanOutputLinkType.Input, input, operands, index * PointerSize, depth, isRoute: true);

                AddReference(input, owner, depth + 1);
            }
        }

        private void AddStorageRow(ulong iterator, int depth)
        {
            if (!memory.TryRead(iterator + IteratorLayout.ScanRowsetOffset, out ulong rowset)
                || rowset < TraceAddress.MinimumAddress + RowsetInterfaceOffset)
            {
                return;
            }

            Add(PlanOutputLinkType.Rowset, rowset, iterator, IteratorLayout.ScanRowsetOffset, depth);

            var rowsetStart = rowset - RowsetInterfaceOffset;

            if (!memory.TryRead(rowsetStart + RowsetRowOffset, out ulong row) || row < TraceAddress.MinimumAddress)
            {
                return;
            }

            Add(PlanOutputLinkType.Row, row, rowsetStart, RowsetRowOffset, depth);

            if (memory.TryRead(row + RowRecordOffset, out ulong record) && isRecord(record))
            {
                Add(PlanOutputLinkType.Record, row + RowRecordOffset, row, RowRecordOffset, depth);
            }
        }

        private bool IsBatch(ulong iterator) => getClassName(iterator)?.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal) == true;

        private bool IsReference(ulong address)
            => getClassName(address)?.StartsWith(ReferenceClassPrefix, StringComparison.Ordinal) == true;

        private bool AddMap(ulong iterator)
        {
            if (!memory.TryRead(iterator + IteratorLayout.ExecutionContextOffset, out ulong context)
                || !memory.TryRead(context + QueryContextOffset, out ulong queryContext)
                || !memory.TryRead(queryContext + IteratorMapOffset, out ulong map))
            {
                return false;
            }

            Add(PlanOutputLinkType.ExecutionContext, context, iterator, IteratorLayout.ExecutionContextOffset, 0, isRoute: true);

            Add(PlanOutputLinkType.QueryContext, queryContext, context, QueryContextOffset, 0, isRoute: true);

            Add(PlanOutputLinkType.IteratorMap, map, queryContext, IteratorMapOffset, 0, isRoute: true);

            return true;
        }

        private ulong? GetIterator(ulong planObject, int depth)
        {
            var map = GetAddress(PlanOutputLinkType.IteratorMap) ?? 0;

            var buckets = map == 0 ? 0 : Math.Min((ulong)getSize(map) / PointerSize, (ulong)MaximumEntries);

            for (var bucket = 0UL; bucket < buckets; bucket++)
            {
                var entries = new List<(ulong Entry, ulong Source, ulong Offset)>();

                var source = map;

                var offset = bucket * PointerSize;

                while (entries.Count < MaximumEntries && memory.TryRead(source + offset, out ulong entry) && entry != 0)
                {
                    entries.Add((entry, source, offset));

                    if (memory.TryRead(entry + MapEntryPlanObjectOffset, out ulong found)
                        && found == planObject
                        && memory.TryRead(entry + MapEntryIteratorOffset, out ulong iterator))
                    {
                        foreach (var (mapEntry, mapSource, mapOffset) in entries)
                        {
                            Add(PlanOutputLinkType.MapEntry, mapEntry, mapSource, mapOffset, depth, isRoute: true);
                        }

                        Add(PlanOutputLinkType.Iterator, iterator, entry, MapEntryIteratorOffset, depth, isRoute: true);

                        return iterator;
                    }

                    source = entry;

                    offset = 0;
                }
            }

            foreach (var planIterator in planIterators)
            {
                if (Read(planIterator + Layout.RowIteratorPlanObjectOffset) != planObject)
                {
                    continue;
                }

                var (iterator, offset) = GetWrapped(planIterator) ?? (planIterator, 0UL);

                Add(PlanOutputLinkType.Iterator, iterator, planIterator, offset, depth, isRoute: true);

                return iterator;
            }

            return null;
        }

        private void AddInputPath(ulong owner, ulong target, int depth)
        {
            var path = new List<(PlanOutputLinkType Type, ulong Address, ulong Source, ulong Offset)>();

            if (!FindInputPath(owner, target, path, 0))
            {
                return;
            }

            foreach (var (type, address, source, offset) in path)
            {
                Add(type, address, source, offset, depth);
            }
        }

        private bool FindInputPath(ulong iterator,
                                   ulong target,
                                   List<(PlanOutputLinkType Type, ulong Address, ulong Source, ulong Offset)> path,
                                   int level)
        {
            if (level >= MaximumLevels
                || !memory.TryRead(iterator + IteratorLayout.InputsOffset, out ulong inputs)
                || inputs < TraceAddress.MinimumAddress)
            {
                return false;
            }

            var size = getSize(inputs);

            var count = size > 0 ? Math.Min(size / (int)PointerSize, MaximumEntries) : MaximumEntries;

            for (var index = 0; index < count; index++)
            {
                var inputOffset = (ulong)index * PointerSize;

                if (!memory.TryRead(inputs + inputOffset, out ulong input) || input < TraceAddress.MinimumAddress)
                {
                    break;
                }

                var mark = path.Count;

                path.Add((PlanOutputLinkType.Inputs, inputs, iterator, (ulong)IteratorLayout.InputsOffset));

                var child = input;

                if (GetWrapped(input) is { } wrapped)
                {
                    path.Add((PlanOutputLinkType.ProfilingIterator, input, inputs, inputOffset));

                    path.Add((PlanOutputLinkType.Iterator, wrapped.Iterator, input, wrapped.Offset));

                    child = wrapped.Iterator;
                }
                else
                {
                    path.Add((PlanOutputLinkType.Iterator, input, inputs, inputOffset));
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
            if (!memory.TryRead(profiling + Layout.RowIteratorPlanObjectOffset, out ulong planObject) || planObject == 0)
            {
                return null;
            }

            for (var offset = 0UL; offset < WrappedSearchLength; offset += PointerSize)
            {
                if (memory.TryRead(profiling + offset, out ulong found)
                    && found != profiling
                    && found != planObject
                    && getSize(found) > 0
                    && Read(found + Layout.RowIteratorPlanObjectOffset) == planObject)
                {
                    return (found, offset);
                }
            }

            return null;
        }

        private void AddJoinValue(ulong reference, ulong iterator, int depth)
        {
            if (!memory.TryRead(reference + JoinSideOffset, out ushort side)
                || side > 1
                || !memory.TryRead(reference + JoinColumnOffset, out int column)
                || column is < 0 or >= MaximumOutputs)
            {
                return;
            }

            var valuesOffset = IteratorLayout.BuildOutputValuesOffset + side * PointerSize;

            var valueOffset = (ulong)column * PointerSize;

            if (!memory.TryRead(iterator + valuesOffset, out ulong values)
                || !memory.TryRead(values + valueOffset, out ulong value))
            {
                return;
            }

            Add(PlanOutputLinkType.OutputValues, values, iterator, valuesOffset, depth);

            Add(PlanOutputLinkType.Value, value, values, valueOffset, depth);

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

            if (!memory.TryRead(value + TraceHashTableLayout.SwitchPrimaryOffset, out ulong primary))
            {
                return;
            }

            if (AddReader(primary, value, TraceHashTableLayout.SwitchPrimaryOffset, depth, readers) is { } reader)
            {
                AddRow(readers[^1], reader, depth);
            }

            if (!memory.TryRead(value + TraceHashTableLayout.SwitchInputOffset, out ulong input) || !IsReference(input))
            {
                return;
            }

            Add(PlanOutputLinkType.Input, input, value, TraceHashTableLayout.SwitchInputOffset, depth, isRoute: true);

            var entry = AddReference(input, iterator, depth + 1);

            if (readers.Count == 1
                && entry is { } inputEntry
                && memory.TryRead(primary + CachedSwitchValueOffset, out ulong inner)
                && Read(inner + ValueEntryOffset) == inputEntry)
            {
                Add(PlanOutputLinkType.InputValue, inner, primary, CachedSwitchValueOffset, depth);

                Add(PlanOutputLinkType.BufferEntry, inputEntry, inner, ValueEntryOffset, depth);
            }

            AddInputValue(iterator, entry, readers, depth + 1);
        }

        private (int Slot, int? Offset, ulong Accessor, ulong SlotOffset)? AddReader(ulong address,
                                                                                   ulong source,
                                                                                   ulong sourceOffset,
                                                                                   int depth,
                                                                                   List<ulong> readers)
        {
            if (getClassName(address) == CachedSwitchClass)
            {
                if (!memory.TryRead(address + CachedSwitchValueOffset, out ulong inner) || getClassName(inner) == CachedSwitchClass)
                {
                    return null;
                }

                Add(PlanOutputLinkType.Reader, address, source, sourceOffset, depth);

                readers.Add(address);

                return AddReader(inner, address, CachedSwitchValueOffset, depth, readers);
            }

            if (ReadReader(address) is not { } reader)
            {
                return null;
            }

            Add(PlanOutputLinkType.Reader, address, source, sourceOffset, depth, reader.Slot, reader.Offset);

            readers.Add(address);

            return reader;
        }

        private (int Slot, int? Offset, ulong Accessor, ulong SlotOffset)? ReadReader(ulong address)
        {
            switch (getClassName(address))
            {
                case TraceHashTableLayout.ValueClass:
                    return (ReadSlot(address + TraceHashTableLayout.ValueSlotOffset),
                            memory.TryRead(address + TraceHashTableLayout.ValueRecordOffset, out int offset) ? offset : (int?)null,
                            0,
                            TraceHashTableLayout.ValueSlotOffset);
                case StorageValueClass:
                    memory.TryRead(address + StorageValueAccessorOffset, out ulong accessor);

                    return (ReadSlot(address + StorageValueSlotOffset), null, accessor, StorageValueSlotOffset);
                default:
                    return null;
            }
        }

        private int ReadSlot(ulong address) => memory.TryRead(address, out int slot) ? slot : -1;

        private void AddRow(ulong reader, (int Slot, int? Offset, ulong Accessor, ulong SlotOffset) info, int depth)
        {
            if (info.Slot < 0 || GetRowContext(depth) is not { } rowContext)
            {
                return;
            }

            var slotOffset = (ulong)info.Slot * PointerSize;

            var rowSlot = rowContext + slotOffset;

            Add(PlanOutputLinkType.RowContext, rowContext, reader, info.SlotOffset, depth, info.Slot);

            Add(PlanOutputLinkType.RowSlot, rowSlot, rowContext, slotOffset, depth, info.Slot);

            if (info.Accessor != 0
                && memory.TryRead(rowSlot, out ulong rowset)
                && memory.TryRead(rowset, out ulong row)
                && memory.TryRead(row + RowRecordOffset, out ulong record)
                && isRecord(record))
            {
                Add(PlanOutputLinkType.Rowset, rowset, rowContext, slotOffset, depth, info.Slot);

                Add(PlanOutputLinkType.Row, row, rowset, 0, depth);

                Add(PlanOutputLinkType.Record,
                    row + RowRecordOffset,
                    row,
                    RowRecordOffset,
                    depth,
                    info.Slot,
                    GetColumnOffset(info.Accessor));
            }
        }

        private ulong? GetRowContext(int depth)
        {
            if (Links.FindIndex(l => l is { Type: PlanOutputLinkType.RowContext, IsRoute: true }) is >= 0 and var index)
            {
                return Links[index].Address;
            }

            if (GetAddress(PlanOutputLinkType.QueryContext) is not { } queryContext
                || !memory.TryRead(queryContext + QueryScanOffset, out ulong queryScan)
                || !memory.TryRead(queryScan + QueryScanContextOffset, out ulong scanContext)
                || !memory.TryRead(scanContext + StatementOffset, out ulong statement)
                || !memory.TryRead(statement + RowContextOffset, out ulong rowContext)
                || rowContext < TraceAddress.MinimumAddress)
            {
                return null;
            }

            Add(PlanOutputLinkType.QueryScan, queryScan, queryContext, QueryScanOffset, depth, isRoute: true);

            Add(PlanOutputLinkType.QueryScanContext, scanContext, queryScan, QueryScanContextOffset, depth, isRoute: true);

            Add(PlanOutputLinkType.Statement, statement, scanContext, StatementOffset, depth, isRoute: true);

            Add(PlanOutputLinkType.RowContext, rowContext, statement, RowContextOffset, depth, isRoute: true);

            return rowContext;
        }

        private ulong? AddBufferEntry(ulong reference, ulong iterator, int depth)
        {
            if (!memory.TryRead(reference + OrdinalColumnOffset, out int column)
                || column is < 0 or >= MaximumOutputs
                || !memory.TryRead(iterator + AccessorBindingLayout.ScanRowBufferOffset, out ulong rowBuffer)
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

            Add(PlanOutputLinkType.RowBuffer, rowBuffer, iterator, AccessorBindingLayout.ScanRowBufferOffset, depth);

            Add(PlanOutputLinkType.BufferEntry, entry, rowBuffer, entryOffset, depth, offset: GetRecordOffset(iterator, (int)entryOffset));

            return entry;
        }

        private void AddInputValue(ulong iterator, ulong? entry, List<ulong> readers, int depth)
        {
            for (var side = 0UL; side < 2; side++)
            {
                var valuesOffset = IteratorLayout.BuildInputValuesOffset + side * PointerSize;

                if (!memory.TryRead(iterator + valuesOffset, out ulong values) || values < TraceAddress.MinimumAddress)
                {
                    continue;
                }

                var count = Math.Min(getSize(values) / (int)PointerSize, MaximumEntries);

                for (var index = 0; index < count; index++)
                {
                    var valueOffset = (ulong)index * PointerSize;

                    if (!memory.TryRead(values + valueOffset, out ulong value))
                    {
                        continue;
                    }

                    var isEntry = entry is { } target
                                  && memory.TryRead(value + ValueEntryOffset, out ulong pointer)
                                  && pointer == target;

                    if (!isEntry && !readers.Contains(value))
                    {
                        continue;
                    }

                    Add(PlanOutputLinkType.InputValues, values, iterator, valuesOffset, depth);

                    Add(PlanOutputLinkType.InputValue, value, values, valueOffset, depth);

                    if (isEntry && entry is { } entryAddress)
                    {
                        Add(PlanOutputLinkType.BufferEntry, entryAddress, value, ValueEntryOffset, depth);
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

        private ulong Read(ulong address) => memory.TryRead(address, out ulong value) ? value : 0;

        private ulong? GetAddress(PlanOutputLinkType type)
            => Links.FindIndex(l => l.Type == type) is >= 0 and var index ? Links[index].Address : null;

        private void Add(PlanOutputLinkType type,
                         ulong address,
                         ulong source,
                         ulong sourceOffset,
                         int depth,
                         int slot = -1,
                         int? offset = null,
                         bool isRoute = false)
            => Links.Add(new PlanOutputLink(type, address, position)
            {
                Source = source,
                SourceOffset = (int)sourceOffset,
                Depth = depth,
                Slot = slot,
                Offset = offset,
                IsRoute = isRoute
            });
    }
}
