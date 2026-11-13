using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

public sealed partial class MemoryHistoryViewModel(Action<CallTreeNode, int> openCall) : ObservableObject
{
    private const string IteratorObject = "Iterator";

    private const int StatementNodeId = -1;

    private const string UnknownWriter = "Unknown Writer";

    private const string UnknownObject = "Unknown Object";

    private const string AllocationSuffix = " Allocation";

    private const string MemoryObjectPageSuffix = " Page";

    private const string BufferPoolPrefix = "Buffer Pool: ";

    private const string BufferFramePrefix = "Buffer Pool Frame: ";

    private const string BufferSuffix = " BUF";

    private const string BufferObject = "BUF";

    private const string SortPageObject = "Sort Page";

    private const string WorkfilePageObject = "Workfile Page";

    private static readonly Dictionary<ushort, string> SystemDatabases = new()
    {
        [1] = "master",
        [2] = "tempdb",
        [3] = "model",
        [4] = "msdb",
        [32767] = "Resource"
    };

    [ObservableProperty]
    private IReadOnlyList<TraceMemoryBlock>? _memoryBlocks;

    [ObservableProperty]
    private TraceBufferLatch[]? _bufferLatches;

    [ObservableProperty]
    private bool _isAvailable;

    public event EventHandler? WatchesChanged
    {
        add => WatchStore.Changed += value;
        remove => WatchStore.Changed -= value;
    }

    public MemoryHistoryStore Store => Trace?.MemoryHistory ?? MemoryHistoryStore.Empty;

    public TraceCallStore? CallStore => Trace?.CallStore;

    public TraceTimeline? Timeline => Trace?.Timeline;

    public TraceTimeline? QueryTimeline => Trace?.QueryTimeline;

    public MemoryHistoryBatchResolver? Batches { get; private set; }

    public TracePosition WindowStart
        => ScopeStart is { } start && start > (Window?.Start.Position ?? default) ? start : Window?.Start.Position ?? default;

    public TracePosition WindowEnd
        => ScopeEnd is { } end && end < (Window?.End.Position ?? TracePosition.Unknown)
            ? end
            : Window?.End.Position ?? TracePosition.Unknown;

    public TraceScale Scale => Window?.Scale ?? TraceScale.Default;

    public TracePosition ShutdownStart { get; private set; } = TracePosition.Unknown;

    private Action<CallTreeNode, int> OpenCall { get; } = openCall;

    private TraceResult? Trace { get; set; }

    private TraceTimeline? Window { get; set; }

    private TracePosition? ScopeStart { get; set; }

    private TracePosition? ScopeEnd { get; set; }

    private IReadOnlyDictionary<ulong, IteratorTarget> Iterators { get; set; } = new Dictionary<ulong, IteratorTarget>();

    private Dictionary<ulong, int> IteratorBlocks { get; set; } = [];

    private HexWatchStore WatchStore { get; } = new();

    private Dictionary<(ulong Batch, int Vector), SqlDbType> VectorTypes { get; } = [];

    private WorkfileLayoutResolver? Workfiles { get; set; }

    private Func<PlanNodeIdentifier, PlanNode?>? ResolvePlanNode { get; set; }

    private Dictionary<int, string?> PointerTargets { get; } = [];

    private Dictionary<bool, DataPathMap> DataPathMaps { get; } = [];

    private MagicBytesMatcher? MagicBytes { get; set; }

    private AddressMap? Map { get; set; }

    private short DatabaseId { get; set; }

    public void SetTrace(TraceResult? trace,
                         IReadOnlyList<EngineEvent> events,
                         DatabaseSource database,
                         Func<PlanNodeIdentifier, PlanNode?> resolvePlanNode)
    {
        Trace = trace;

        ResolvePlanNode = resolvePlanNode;

        DatabaseId = database.DatabaseId;

        VectorTypes.Clear();

        PointerTargets.Clear();

        lock (DataPathMaps)
        {
            DataPathMaps.Clear();
        }

        IsAvailable = Store.Blocks.Count > 0;

        IteratorBlocks = GetIteratorBlocks();

        Iterators = trace is null
            ? new Dictionary<ulong, IteratorTarget>()
            : IteratorTarget.Build(trace.CallTree, events, trace.PlanLink, trace.OperatorFrames);

        Batches = trace is null
            ? null
            : new MemoryHistoryBatchResolver(Store, b => GetMapLabel(Store.Blocks[b]), b => GetAllocatingCall(b)?.Symbol);

        MagicBytes = trace is null ? null : new MagicBytesMatcher(Store);

        Map = trace?.AddressMap;

        Workfiles = trace is null ? null : new WorkfileLayoutResolver(database, GetPlanNode);

        ScopeStart = null;

        ScopeEnd = null;

        SetWindow(null);
    }

    public void SetWindow(TraceTimeline? window)
    {
        Window = window;

        ShutdownStart = window?.GetFirstCallStart(TraceTimeline.ShutdownSymbol) ?? TracePosition.Unknown;

        ApplyWindow();
    }

    public void SetScope(TracePosition? start, TracePosition? end)
    {
        ScopeStart = start;

        ScopeEnd = end;

        ApplyWindow();
    }

    private void ApplyWindow()
    {
        MemoryBlocks = BuildMemoryBlocks();

        BufferLatches = BuildBufferLatches();
    }

    public void ShowCall(CallValue value) => OpenCall(value.Node, value.CallIndex);

    public int GetBlock(ulong address, TracePosition position) => Store.GetBlock(address, position);

    public string? GetPointerTarget(ulong address, TracePosition position)
    {
        var block = Store.GetBlock(address, position);

        if (block < 0)
        {
            return null;
        }

        if (!PointerTargets.TryGetValue(block, out var label))
        {
            label = GetDisplayLabel(block) is { Length: > 0 } name ? name : null;

            PointerTargets[block] = label;
        }

        var offset = address - Store.Blocks[block].Address;

        return label is null || offset == 0 ? label : $"{label} +0x{offset:X}";
    }

    public TraceBufferPool BufferPool => Trace?.BufferPool ?? TraceBufferPool.Empty;

    public AddressMap AddressMap => Map ?? AddressMap.Empty;

    public IReadOnlyDictionary<ulong, IteratorTarget> IteratorTargets => Iterators;

    public PlanOutputRecordReader? Records { get; init; }

    public IReadOnlyList<PlanOutputColumn> GetOutputColumns(int nodeId) => Trace?.OutputColumns.GetValueOrDefault(nodeId) ?? [];

    public PlanOutputPath GetOutputPath(int nodeId, PlanOutputColumn column)
        => Trace?.GetOutputPath(nodeId, column) ?? PlanOutputPath.Empty;

    public string? GetUnexpectedWriter(PlanOutputLink link) => Trace?.GetUnexpectedWriter(link);

    public DataPathMap GetDataPathMap(bool isPlanObjectIncluded = false)
    {
        lock (DataPathMaps)
        {
            if (!DataPathMaps.TryGetValue(isPlanObjectIncluded, out var map))
            {
                map = Trace is { } trace && FindStatement() is { } statement
                    ? DataPathBuilder.Build(trace, statement, Iterators, isPlanObjectIncluded)
                    : DataPathMap.Empty;

                DataPathMaps[isPlanObjectIncluded] = map;
            }

            return map;
        }
    }

    public Task<string?> ReadRecordAsync(PlanOutputRecord record) => Records?.ReadAsync(record) ?? Task.FromResult<string?>(null);

    public List<string> GetOutputColumnNames(ulong target, int count)
    {
        var columns = GetPlanNode(target)?.OutputColumns is { } found && found.Count == count ? found : null;

        return [.. Enumerable.Range(0, count).Select(i => columns?[i].Column is { Length: > 0 } name ? name : $"Column {i + 1}")];
    }

    public string? GetStatementResult(TracePosition position, SqlDbType type) => Trace?.GetStatementResult(position, type);

    public (TracePosition From, TracePosition To)? GetRowRange(ulong iterator) => Trace?.GetRowRange(iterator);

    public PlanOutputRecord? GetOutputRecord(MemoryHistoryReader memory, PlanOutputStage stage)
        => Trace is { } trace ? PlanOutputValueReader.ReadRecord(memory, trace.BufferPool, memory.Position, stage) : null;

    public PlanNode? GetStatement(PlanNodeIdentifier node)
        => ResolvePlanNode?.Invoke(new PlanNodeIdentifier(node.PlanHandleId, StatementNodeId)) is { IsStatement: true } statement
            ? statement
            : null;

    public string? GetStatementName(PlanNodeIdentifier node) => GetStatement(node)?.PhysicalOperator;

    private PlanNode? FindStatement()
        => Iterators.Values
                    .Select(t => t.Operator?.PlanNodeIdentifier)
                    .OfType<PlanNodeIdentifier>()
                    .FirstOrDefault() is { } identifier
            ? GetStatement(identifier)
            : null;

    public PlanNode? GetPlanNode(ulong iterator)
        => ResolvePlanNode is { } resolve
           && Iterators.TryGetValue(iterator, out var target)
           && target.Operator?.PlanNodeIdentifier is { } identifier
            ? resolve(identifier)
            : null;

    public TraceBufferReference? GetBufferPage(ulong address, TracePosition position) => Trace?.BufferPool.GetReference(address, position);

    public string GetBufferPageLabel(TraceBufferReference reference)
    {
        var buffer = reference.IsBuffer ? BufferSuffix : string.Empty;

        var offset = reference.Offset == 0 ? string.Empty : $" +0x{reference.Offset:X}";

        var prefix = reference.IsBuffer ? BufferPoolPrefix : BufferFramePrefix;

        return $"{prefix}{GetDatabasePrefix(reference)}{reference.Page}{buffer}{offset}";
    }

    public bool IsQueryDatabase(TraceBufferReference reference) => reference.DatabaseId == DatabaseId;

    public string GetBufferPageName(TraceBufferReference reference) => $"{GetDatabasePrefix(reference)}{reference.Page}";

    public int? GetBufferSlot(TraceBufferReference reference)
        => reference.IsBuffer ? null : Trace?.BufferPool.GetLayout(reference.Page, reference.DatabaseId)?.GetSlot(reference.Offset);

    public bool IsBuffer(int block) => Trace?.BufferPool.IsBuffer(Store.Blocks[block].Address) == true;

    public bool IsPage(int block)
        => Store.Blocks[block].Size == PageData.Size && GetMapLabel(Store.Blocks[block]) == WorkfilePageReader.ClassName;

    public bool IsAccessorBindings(int block) => GetMapLabel(Store.Blocks[block]) == AccessorBindingLayout.ClassName;

    public bool IsColumnBindings(int block) => GetMapLabel(Store.Blocks[block]) == AccessorBindingLayout.ColumnsClassName;

    public MemoryHistoryArray? GetArray(int block)
        => IsColumnBindings(block) && AccessorBindingLayout.GetColumnCount(Store.Blocks[block].Size) is > 0 and var count
            ? new MemoryHistoryArray(AccessorBindingLayout.ColumnSize, count)
            : null;

    public RowRecordLayout? GetWorkfileLayout(int block) => GetPageIterator(block) is { } iterator ? Workfiles?.Get(iterator) : null;

    public RowRecordLayout? GetWorkfileLayout(ulong iterator, IReadOnlyList<ColumnBinding> bindings, bool isProbeSide)
        => Workfiles?.Get(iterator, bindings, isProbeSide);

    public SortRecordLayout? GetSortLayout(int block)
        => GetPageIterator(block) is { } iterator ? Workfiles?.GetSort(iterator, () => ReadScanBindings(iterator, block)) : null;

    public SqlDbType? GetVectorType(ulong batch, int vector) => VectorTypes.TryGetValue((batch, vector), out var type) ? type : null;

    public void SetVectorType(ulong batch, int vector, SqlDbType? type)
    {
        if (type is { } found)
        {
            VectorTypes[(batch, vector)] = found;
        }
        else
        {
            VectorTypes.Remove((batch, vector));
        }
    }

    public string GetObjectName(int block) => GetDisplayLabel(block) is { Length: > 0 } name ? name : UnknownObject;

    public int GetIteratorBlock(ulong address) => IteratorBlocks.GetValueOrDefault(address, -1);

    public IteratorTarget? GetIteratorTarget(int block)
        => Store.Blocks[block] is var info
           && Iterators.TryGetValue(info.Address, out var target)
           && (GetIteratorBlock(info.Address) == block || target.Type == IteratorTargetType.PlanObject)
            ? target
            : null;

    public string? GetOperatorName(int block)
        => Store.Blocks[block] is var info
           && Iterators.TryGetValue(info.Address, out var target)
           && target.Operator is { PlanNodeIdentifier: not null }
           && (GetIteratorBlock(info.Address) == block || target.Type == IteratorTargetType.PlanObject)
            ? $"{target.TypeName} › {target.Label}"
            : null;

    public string GetTypeName(int block)
    {
        if (IsBuffer(block))
        {
            return BufferObject;
        }

        if (IsPage(block))
        {
            return WorkfilePageReader.GetPageType(Store, block) == PageType.Sort ? SortPageObject : WorkfilePageObject;
        }

        return GetObjectName(block);
    }

    public CallTreeNode? GetWriter(int callNodeIndex) => callNodeIndex >= 0 ? Trace?.Timeline.GetNode(callNodeIndex) : null;

    public Windows.UI.Color GetCategoryColour(int callNodeIndex)
        => ColourHelpers.FromHex(GetWriter(callNodeIndex)?.CategoryColour) ?? Colors.Gray;

    public Windows.UI.Color? GetAllocationColour(int block) => ColourHelpers.FromHex(GetAllocatingCall(block)?.CategoryColour);

    public string GetWriterLabel(int callNodeIndex) => GetWriterLabel(GetWriter(callNodeIndex));

    public TraceCallLink GetCallLink(uint thread, TracePoint point, CallTreeNode? node)
    {
        if (Trace?.Timeline is not { } timeline
            || timeline.GetStack(point.Position).FirstOrDefault(s => s.ThreadId == thread) is not { } stack)
        {
            return new TraceCallLink(point, node, -1);
        }

        TraceSpan? found = null;

        foreach (var call in stack.Calls)
        {
            if (node is null || ReferenceEquals(timeline.GetNode(call.CallNodeIndex), node))
            {
                found = call;
            }
        }

        return found is { } span
            ? new TraceCallLink(point, timeline.GetNode(span.CallNodeIndex), span.CallIndex)
            : new TraceCallLink(point, node, -1);
    }

    public CallTreeNode? GetAllocatingCall(int block)
    {
        if (Trace is not { } trace)
        {
            return null;
        }

        if (!Store.Blocks[block].IsInline)
        {
            return Store.GetAllocatingCall(block, trace.Timeline);
        }

        var writes = Store.GetWrites(block);

        return writes.Count > 0 ? trace.Timeline.GetNode(writes[0].CallNodeIndex) : null;
    }

    public ObservableCollection<HexWatch> GetWatches(int block) => WatchStore.GetWatches(GetWatchName(block));

    public string GetWatchName(int block)
    {
        var label = GetObjectLabel(block);

        return label.Length > 0 && label != IteratorObject ? label : $"0x{Store.Blocks[block].Address:X16}";
    }

    public Dictionary<string, HexWatch[]> GetWatchSnapshot() => WatchStore.GetSnapshot();

    public int RemoveTemporaryWatches() => WatchStore.RemoveTemporary();

    private List<TraceMemoryBlock> BuildMemoryBlocks()
    {
        var blocks = new List<TraceMemoryBlock>();

        if (Trace is null || Window is null)
        {
            return blocks;
        }

        for (var index = 0; index < Store.Blocks.Count; index++)
        {
            var block = Store.Blocks[index];

            if (!IsTracked(index))
            {
                continue;
            }

            var (first, count) = Store.GetChangedWriteRange(index, WindowStart, WindowEnd);

            var (firstRead, readCount) = Store.GetReadRange(index, WindowStart, WindowEnd);

            if (count == 0 && readCount == 0)
            {
                continue;
            }

            var label = GetDisplayLabel(index);

            blocks.Add(new TraceMemoryBlock(index,
                                            label.Length > 0 ? label : UnknownObject,
                                            block.Address,
                                            block.Size,
                                            block.From,
                                            block.To,
                                            new TraceMemoryWrites(Store, first, count),
                                            IsBuffer(index),
                                            IsBuffer(index) ? GetBufferPages(block.Address) : null,
                                            readCount > 0 ? new TraceMemoryWrites(Store, firstRead, readCount, isReads: true) : null,
                                            block.InstanceCount > 0 ? Store.GetInstances(index) : null,
                                            block.IsStack));
        }

        return blocks;
    }

    private TraceBufferLatch[] BuildBufferLatches()
    {
        var latches = new List<TraceBufferLatch>();

        if (Trace is null || Window is null)
        {
            return [];
        }

        for (var index = 0; index < Store.Blocks.Count; index++)
        {
            if (!IsBuffer(index))
            {
                continue;
            }

            var label = GetObjectName(index);

            foreach (var (write, state) in BufferDescriptor.GetLatchStates(Store, index))
            {
                var point = Store.GetPoint(write);

                if (point.Position < WindowStart || point.Position > WindowEnd)
                {
                    continue;
                }

                latches.Add(new TraceBufferLatch(point,
                                                 index,
                                                 Store.Blocks[index].Address,
                                                 label,
                                                 BufferDescriptor.GetLatchState(state),
                                                 Store.GetWrite(write).CallNodeIndex >= 0));
            }
        }

        return [.. latches.OrderBy(l => l.Point.Position)];
    }

    private bool IsTracked(int block)
        => Store.IsCreatedWithin(block, Window?.Start.Position ?? default, Window?.End.Position ?? TracePosition.Unknown);

    private Dictionary<ulong, int> GetIteratorBlocks()
    {
        var blocks = new Dictionary<ulong, int>();

        for (var block = 0; block < Store.Blocks.Count; block++)
        {
            if (Store.Blocks[block] is { IsIterator: true } info
                && (!blocks.TryGetValue(info.Address, out var current) || Store.Blocks[current].FromPosition < info.FromPosition))
            {
                blocks[info.Address] = block;
            }
        }

        return blocks;
    }

    private string GetDisplayLabel(int block)
        => GetObjectLabel(block) is { Length: > 0 } label
            ? label
            : GetAllocatingCall(block)?.Symbol is { Length: > 0 } symbol
                ? Store.Blocks[block].IsStack ? symbol : symbol + AllocationSuffix
                : string.Empty;

    private string GetObjectLabel(int block)
        => GetBufferLabel(block)
           ?? Batches?.GetPart(block)?.Name
           ?? MagicBytes?.GetClassName(block)
           ?? GetArrayLabel(block)
           ?? GetMemoryObjectPageLabel(Store.Blocks[block])
           ?? GetVtableLabel(Store.Blocks[block])
           ?? GetMapLabel(Store.Blocks[block])
           ?? (Store.Blocks[block].IsIterator ? IteratorObject : string.Empty);

    private string? GetArrayLabel(int block)
        => Store.GetArray(block) is { } array
           && Store.Blocks[block] is var allocation
           && (Map?.GetVtableClassName(array.FirstElement, allocation.FromPosition, allocation.ToPosition)
               ?? Map?.GetClassName(array.FirstElement, allocation.FromPosition, allocation.ToPosition)) is { } element
            ? $"{element}[{array.Count}]"
            : null;


    private PageAddress[] GetBufferPages(ulong buffer)
        => Trace is { } trace ? [.. trace.BufferPool.GetResidencies(buffer).Select(r => r.Page).Distinct()] : [];

    private string? GetBufferLabel(int block)
    {
        if (!IsBuffer(block))
        {
            return null;
        }

        var buffer = Store.Blocks[block];

        return GetBufferPage(buffer.Address, buffer.FromPosition) is { } reference
            ? $"{BufferObject} {GetBufferPageName(reference)}"
            : BufferObject;
    }

    private string? GetMapLabel(MemoryHistoryBlock block) => Trace?.AddressMap.GetLabel(block.Address, block.FromPosition, block.ToPosition);

    private string? GetVtableLabel(MemoryHistoryBlock block)
        => Trace?.AddressMap.GetVtableClassName(block.Address, block.FromPosition, block.ToPosition);

    private string? GetMemoryObjectPageLabel(MemoryHistoryBlock block)
        => Trace is { } trace
           && trace.MemoryObjects.GetOwner(block.Address, block.FromPosition) is { } owner
           && block.Size == owner.PageSize
           && block.Address % (ulong)owner.PageSize == 0
           && (owner.Name ?? trace.AddressMap.GetEntry(owner.Address, block.FromPosition)?.ClassName) is { } memoryObject
            ? $"{memoryObject}{MemoryObjectPageSuffix}"
            : null;

    private List<ColumnBinding> ReadScanBindings(ulong iterator, int block)
    {
        var writes = Store.GetWrites(block);

        var memory = new MemoryHistoryReader(Store, writes.Count > 0 ? writes[^1].Position : Store.Blocks[block].FromPosition);

        return AccessorBindingLayout.ReadScanColumns(memory, iterator);
    }

    private ulong? GetPageIterator(int block)
    {
        foreach (var write in Store.GetWrites(block))
        {
            if (Store.GetIterator(write) is not 0 and var iterator)
            {
                return iterator;
            }
        }

        return null;
    }

    private static string GetWriterLabel(CallTreeNode? writer) => writer?.Symbol ?? UnknownWriter;

    private string GetDatabasePrefix(TraceBufferReference reference)
        => IsQueryDatabase(reference) ? string.Empty : $"{GetDatabaseName(reference.DatabaseId)} ";

    private static string GetDatabaseName(ushort databaseId)
        => SystemDatabases.TryGetValue(databaseId, out var name) ? name : $"Database {databaseId}";
}
