using System.Collections.Concurrent;
using System.Data;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Compilation;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallTree CallTree, TraceStore Store) : IRunExtra
{
    internal const int ActivityBuckets = 96;

    private const int LiteralBytes = 256;

    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    public OperatorFrameMap OperatorFrames { get; init; } = OperatorFrameMap.Empty;

    internal IReadOnlyList<IteratorPlanNode> PlanNodes { get; init; } = [];

    internal IReadOnlyList<IteratorPlanNode> LinkedPlanNodes
        => field ??= PlanLink.PlanObjects.Count == 0
            ? PlanNodes
            : [.. PlanNodes.Where(p => PlanLink.PlanObjects.ContainsKey(p.PlanObject))];

    public HoBtAccessMap HoBtAccess { get; init; } = HoBtAccessMap.Empty;

    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public TraceOperatorLifetimeList Lifetimes { get; init; } = TraceOperatorLifetimeList.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public IReadOnlyDictionary<ulong, string> VtableClasses { get; init; } = new Dictionary<ulong, string>();

    internal IReadOnlyList<TraceModule> ModuleImages { get; init; } = [];

    internal ModuleSymbolIndex ModuleSymbols { get; init; } = ModuleSymbolIndex.Empty;

    public MemoryObjectMap MemoryObjects { get; init; } = MemoryObjectMap.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    public TracePageReadList PageReads { get; init; } = TracePageReadList.Empty;

    public TracePageAccessList PageAccesses { get; init; } = TracePageAccessList.Empty;

    public MemoryHistoryStore MemoryHistory { get; init; } = MemoryHistoryStore.Empty;

    public Version? ServerVersion { get; init; }

    public TraceSourceTrackMap SourceTracks
    {
        get => field ??= new TraceSourceTrackMap(Store.Timeline, Store.Calls, Lifetimes);
        init;
    }

    public TraceHashTableMap HashTables => field ??= new TraceHashTableMap(this);

    public IReadOnlyDictionary<int, IReadOnlyList<PlanOutputColumn>> OutputColumns
        => field ??= IteratorPlanNode.GetOutputColumns(LinkedPlanNodes);

    public IReadOnlyList<CallTreeNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallTree);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    private IReadOnlyList<CallTreeNode>? Roots { get; set; }

    private TraceTimeline? CroppedTimeline { get; set; }

    private TraceTimeline? ActivityWindow { get; set; }

    private ConcurrentDictionary<int, string?> BlockClasses { get; } = new();

    private ConcurrentDictionary<int, string?> BlockTexts { get; } = new();

    private MemoryListReader? ListReader { get; set; }

    private TraceCompilation? Compilation { get; set; }

    private ConcurrentDictionary<ulong, string?> ModuleLabels { get; } = new();

    private Dictionary<TraceTimeline, Dictionary<CallTreeNode, int[]>> ActivityByWindow { get; } =
        new(ReferenceEqualityComparer.Instance);

    private PlanOutputLinkReader OutputLinks => field ??= new PlanOutputLinkReader(this);

    private PlanOutputResultReader StatementResults => field ??= new PlanOutputResultReader(this);

    private DeclaredTypeMap DeclaredTypes => field ??= DeclaredTypeMap.Build(MemoryHistory, GetMapClass, GetVtableClass);

    public void SetActivityWindow(TraceTimeline window)
    {
        if (ReferenceEquals(ActivityWindow, window))
        {
            return;
        }

        ActivityWindow = window;

        CallTree.SetCallActivity(() => GetCallActivity(window));

        CallTree.ComputeActivity(ActivityBuckets);
    }

    public string? GetVtableClass(int block)
        => BlockClasses.GetOrAdd(block, b => ObjectClassResolver.GetClass(MemoryHistory, VtableClasses, b, []).ClassName);

    public string? GetTextLabel(int block) => BlockTexts.GetOrAdd(block, b => MemoryHistoryTextReader.GetLabel(MemoryHistory, b));

    public MemoryListNode? GetListNode(int block) => (ListReader ??= new MemoryListReader(MemoryHistory, AddressMap)).GetNode(block);

    public TraceCompilation GetCompilation() => Compilation ??= TraceCompilationReader.Read(Store, MemoryHistory, AddressMap);

    public bool IsVtable(ulong value)
        => VtableClasses.ContainsKey(value)
           || ModuleSymbols.Find(value) is { Offset: 0, Symbol: { } symbol } && VtableResolver.GetClassName(symbol) is not null;

    public string? GetVtableBase(ulong value)
        => ModuleSymbols.GetDecoratedName(value) is { } decorated ? VtableResolver.GetBaseName(decorated) : null;

    public string? GetInterfaceName(ulong address, TracePosition position)
        => new MemoryHistoryReader(MemoryHistory, position).TryRead(address, out ulong vtable) ? GetVtableBase(vtable) : null;

    public string? GetDeclaredType(int block) => DeclaredTypes.Get(block);

    public ulong? GetInstructionOperand(ulong instruction, TracePosition position)
    {
        var block = MemoryHistory.GetBlock(instruction, position);

        if (block < 0 || DeclaredTypes.GetSource(block) is not { } source)
        {
            return null;
        }

        var layout = MemoryHistory.Layout;

        var memory = new MemoryHistoryReader(MemoryHistory, position);

        var program = MemoryHistory.Blocks[source].Address;

        if (!memory.TryRead(instruction + (ulong)layout.CodeInstruction.HandlerField.Offset, out ulong handler)
            || !memory.TryRead(instruction + (ulong)layout.CodeInstruction.CountField.Offset, out int count)
            || !memory.TryRead(instruction + (ulong)layout.CodeInstruction.OperandField.Offset, out int operand)
            || !ExpressionInstructionLookup.IsOperandAccess(count, ModuleSymbols.Find(handler)?.Symbol)
            || !memory.TryRead(program + (ulong)layout.Expression.OperandsField.Offset, out ulong operands)
            || !memory.TryRead(program + (ulong)layout.Expression.OperandCountField.Offset, out int operandCount)
            || operand < 0
            || operand >= operandCount
            || !memory.TryRead(operands + (ulong)(operand * layout.Expression.OperandField.Length), out ulong value))
        {
            return null;
        }

        return value;
    }

    public bool IsExpressionInstruction(ulong value)
        => ModuleSymbols.Find(value) is { Offset: 0, Module: var module }
           && ModuleSymbols.GetSignature(value) is { } signature
           && ExpressionInstructionLookup.IsInstruction(module, signature);

    public string? GetModuleLabel(ulong value)
        => ModuleSymbols.Find(value) is { } found
            ? ModuleLabels.GetOrAdd(value, v => (found.IsString ? ReadLiteral(v) : null) ?? found.ToString())
            : null;

    public PlanOutputPath GetOutputPath(int nodeId, PlanOutputColumn column) => OutputLinks.Read(nodeId, column);

    public string? GetStatementResult(TracePosition position, SqlDbType type)
        => StatementResults.Read(position) is { } result
            ? PlanOutputValueReader.ReadVariant(new MemoryHistoryReader(MemoryHistory, result.Position), result.Variant, type)
            : null;

    public string? GetUnexpectedWriter(PlanOutputLink link) => OutputLinks.GetUnexpectedWriter(link);

    public (TracePosition From, TracePosition To)? GetRowRange(ulong iterator) => OutputLinks.GetRowRange(iterator);

    public Dictionary<int, PlanOutputRoute> GetDataPath(IReadOnlyList<PlanOutputRouteOperator> operators, bool isPlanObjectIncluded)
        => OutputLinks.ReadDataPath(operators, isPlanObjectIncluded);

    internal void BuildOutputPaths() => OutputLinks.ReadAll();

    private string? GetMapClass(int block)
        => MemoryHistory.Blocks[block] is { IsStack: false } item
            ? AddressMap.GetVtableClassName(item.Address, item.FromPosition, item.ToPosition)
              ?? AddressMap.GetClassName(item.Address, item.FromPosition, item.ToPosition)
            : null;

    private string? ReadLiteral(ulong address)
    {
        if (ModuleImages.Count == 0)
        {
            return null;
        }

        using var images = new ImageText(ModuleImages);

        Span<byte> bytes = stackalloc byte[LiteralBytes];

        var count = images.Read(address, bytes, isReadOnly: true);

        return count == 0 ? null : MemoryHistoryTextReader.GetLiteralLabel(bytes[..count]);
    }

    private Dictionary<CallTreeNode, int[]> GetCallActivity(TraceTimeline window)
    {
        lock (ActivityByWindow)
        {
            if (!ActivityByWindow.TryGetValue(window, out var activity))
            {
                activity = window.GetCallActivity(ActivityBuckets);

                ActivityByWindow[window] = activity;
            }

            return activity;
        }
    }

    private TraceTimeline GetQueryTimeline()
        => QueryRoots.Count > 0
            ? Store.Timeline.CropTo(QueryRoots.ToHashSet<CallTreeNode>(ReferenceEqualityComparer.Instance)) ?? Store.Timeline
            : Store.Timeline;
}
