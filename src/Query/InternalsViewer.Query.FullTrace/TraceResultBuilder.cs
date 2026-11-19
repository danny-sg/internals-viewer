using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Replay.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace;

internal sealed class TraceResultBuilder(CallTree callTree,
                                         TraceStore store,
                                         IReadOnlyList<EngineEvent> events,
                                         IReadOnlyList<IteratorPlanNode> planNodes)
{
    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public IReadOnlyDictionary<ulong, MemoryObjectInfo> MemoryClerkObjects { get; init; } = new Dictionary<ulong, MemoryObjectInfo>();

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    public Version? ServerVersion { get; init; }

    public string? SymbolsPath { get; init; }

    public IReadOnlyList<TraceModule> ModuleImages { get; init; } = [];

    public TraceStore Store { get; } = store;

    public IteratorPlanLink PlanLink { get; private set; } = IteratorPlanLink.Empty;

    public OperatorFrameMap OperatorFrames { get; private set; } = OperatorFrameMap.Empty;

    public TraceOperatorLifetimeList Lifetimes { get; private set; } = TraceOperatorLifetimeList.Empty;

    public MemoryObjectMap MemoryObjects { get; private set; } = MemoryObjectMap.Empty;

    private CallTree CallTree { get; } = callTree;

    private IReadOnlyList<EngineEvent> Events { get; } = events;

    private IReadOnlyList<IteratorPlanNode> PlanNodes { get; } = planNodes;

    private bool IsLinked { get; set; }

    public void LinkExecutionPlan()
    {
        if (IsLinked)
        {
            return;
        }

        PlanLink = IteratorPlanLinker.Link(PlanNodes, Events);

        OperatorFrames = OperatorFrameMap.Build(CallTree, Events, PlanLink);

        Lifetimes = IteratorLifetimeBuilder.Build(Store.Timeline,
                                                  Allocation,
                                                  OperatorFrames.OperatorsByInstance,
                                                  PlanNodes,
                                                  QueryTeardown.GetTeardowns(Store.Timeline));

        MemoryObjects = MemoryObjectMap.Build(Store.Calls, CallTree, MemoryClerkObjects);

        IsLinked = true;
    }

    public TraceResult Build(MemoryHistoryStore memoryHistory, TracePageReadList pageReads)
    {
        LinkExecutionPlan();

        var layout = ServerVersion is null ? EngineLayout.Find(memoryHistory, PlanNodes) : EngineLayout.Get(ServerVersion);

        var store = memoryHistory.WithLayout(layout);

        var enclosing = EnclosingOperatorCallIndex.Build(Store.Calls, Lifetimes.OperatorsByInstance);

        var pageAccesses = TracePageAccessList.Build(Store.Calls, store, BufferPool, pageReads, Store.Timeline);

        var (symbols, vtables) = LoadSymbols(store);

        var result = new TraceResult(CallTree, Store)
        {
            PlanLink = PlanLink,
            OperatorFrames = OperatorFrames,
            PlanNodes = PlanOutputReader.Read(PlanNodes, store),
            HoBtAccess = HoBtAccessMap.Build(Store.Calls, CallTree),
            Allocation = Allocation,
            Lifetimes = Lifetimes,
            AddressMap = vtables.Count > 0 ? ObjectClassResolver.Resolve(AddressMap, store, vtables) : AddressMap,
            VtableClasses = vtables,
            ModuleSymbols = symbols,
            ModuleImages = ModuleImages,
            MemoryObjects = MemoryObjects,
            BufferPool = BufferPool.WithLayout(layout),
            PageReads = pageReads,
            PageAccesses = pageAccesses,
            MemoryHistory = store,
            ServerVersion = ServerVersion,
            SourceTracks = new TraceSourceTrackMap(Store.Timeline, Store.Calls, Lifetimes)
            {
                EnclosingCalls = enclosing,
                RowReturns = TraceRowReturnMarkMap.Build(Store.Timeline, Store.Calls, Lifetimes, layout, enclosing),
                RowAccesses = TraceRowAccessTrackBuilder.Build(Store, pageAccesses, BufferPool, Lifetimes, enclosing),
                PageHeaderAccesses = TraceRowAccessTrackBuilder.BuildHeaders(Store, pageAccesses, Lifetimes, enclosing),
                PageFetches = TracePageFetchTrackBuilder.Build(Store, pageAccesses, BufferPool, Lifetimes, enclosing),
                Seeks = TraceSeekTrackBuilder.Build(Store.Timeline, Store.Calls, Lifetimes, enclosing)
            }
        };

        if (store.Blocks.Count > 0)
        {
            result.BuildOutputPaths();
        }

        Store.Calls.ClearColumnCache();

        return result;
    }

    private (ModuleSymbolIndex Symbols, IReadOnlyDictionary<ulong, string> VtableClasses) LoadSymbols(MemoryHistoryStore store)
    {
        if (SymbolsPath is null || store.Blocks.Count == 0)
        {
            return (ModuleSymbolIndex.Empty, new Dictionary<ulong, string>());
        }

        List<(AddressTable<string[]> Table, ulong Address)> vtables = [];

        List<(string Module, ulong Start, SymbolTable Table)> symbols = [];

        try
        {
            foreach (var (module, start) in GetModuleStarts())
            {
                if (ReplayFunctionResolver.IsReplayedModule(module.Name) && SymbolTableCache.Get(SymbolsPath, module) is { } table)
                {
                    vtables.Add((VtableResolver.Read(table), start));

                    symbols.Add((module.Name, start, table));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return (ModuleSymbolIndex.Empty, new Dictionary<ulong, string>());
        }

        var classes = vtables.Count > 0
                      ? VtableResolver.GetUniqueClasses(AddressTable<string[]>.Merge(vtables))
                      : new Dictionary<ulong, string>();

        return (new ModuleSymbolIndex(symbols), classes);
    }

    private Dictionary<FrameModule, ulong> GetModuleStarts()
    {
        var starts = new Dictionary<FrameModule, ulong>();

        foreach (var node in CallTree.GetNodes())
        {
            if (node.Frame is { Address: > 0, Rva: > 0 } frame && frame.Address > frame.Rva)
            {
                starts.TryAdd(frame.Module, frame.Address - frame.Rva);
            }
        }

        return starts;
    }
}
