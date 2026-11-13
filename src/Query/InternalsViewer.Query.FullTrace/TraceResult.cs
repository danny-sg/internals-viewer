using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallTree CallTree, TraceCallStore CallStore, TraceTimeline Timeline) : IRunExtra
{
    internal const int ActivityBuckets = 96;

    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    public OperatorFrameMap OperatorFrames { get; init; } = OperatorFrameMap.Empty;

    internal IReadOnlyList<IteratorPlanNode> PlanNodes { get; init; } = [];

    internal IReadOnlyList<IteratorPlanNode> LinkedPlanNodes
        => field ??= PlanLink.PlanObjects.Count == 0
            ? PlanNodes
            : [.. PlanNodes.Where(p => PlanLink.PlanObjects.ContainsKey(p.PlanObject))];

    internal IReadOnlyList<QueryTeardown> Teardowns { get; init; } = [];

    public HoBtAccessMap HoBtAccess { get; init; } = HoBtAccessMap.Empty;

    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public TraceOperatorLifetimeList Lifetimes { get; init; } = TraceOperatorLifetimeList.Empty;

    public AddressMap AddressMap { get; internal set; } = AddressMap.Empty;

    public MemoryObjectMap MemoryObjects { get; init; } = MemoryObjectMap.Empty;

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    public TracePageReadList PageReads { get; init; } = TracePageReadList.Empty;

    public TracePageAccessList PageAccesses { get; init; } = TracePageAccessList.Empty;

    public MemoryHistoryStore MemoryHistory { get; init; } = MemoryHistoryStore.Empty;

    public TraceSourceTrackMap SourceTracks
    {
        get => field ??= new TraceSourceTrackMap(Timeline, CallStore, Lifetimes);
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

    private Dictionary<TraceTimeline, Dictionary<CallTreeNode, int[]>> ActivityByWindow { get; } =
        new(ReferenceEqualityComparer.Instance);

    private PlanOutputLinkReader OutputLinks => field ??= new PlanOutputLinkReader(this);

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

    public void ResolveObjectClasses(string symbolsPath)
    {
        if (MemoryHistory.Blocks.Count == 0)
        {
            return;
        }

        List<(AddressTable<string[]> Table, ulong Address)> vtables = [];

        foreach (var (module, start) in GetModuleStarts())
        {
            if (ReplayFunctionResolver.IsReplayedModule(module.Name) && SymbolTableCache.Get(symbolsPath, module) is { } table)
            {
                vtables.Add((VtableResolver.Read(table), start));
            }
        }

        if (vtables.Count > 0)
        {
            var classes = VtableResolver.GetUniqueClasses(AddressTable<string[]>.Merge(vtables));

            AddressMap = ObjectClassResolver.Resolve(AddressMap, MemoryHistory, classes);
        }
    }

    public PlanOutputPath GetOutputPath(int nodeId, PlanOutputColumn column) => OutputLinks.Read(nodeId, column);

    public string? GetUnexpectedWriter(PlanOutputLink link) => OutputLinks.GetUnexpectedWriter(link);

    public Dictionary<int, PlanOutputRoute> GetDataPath(IReadOnlyList<PlanOutputRouteOperator> operators, bool isPlanObjectIncluded)
        => OutputLinks.ReadDataPath(operators, isPlanObjectIncluded);

    internal void BuildOutputPaths() => OutputLinks.ReadAll();

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
            ? Timeline.CropTo(QueryRoots.ToHashSet<CallTreeNode>(ReferenceEqualityComparer.Instance)) ?? Timeline
            : Timeline;
}
