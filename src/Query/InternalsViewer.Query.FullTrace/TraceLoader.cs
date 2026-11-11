using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace;

public sealed class TraceLoader(ILogger<TraceLoader> logger, ITimeTravelRecorder? timeTravelRecorder = null)
{
    private const string OpeningStep = "Opening Full Trace";

    private const string SymbolsStep = "Resolving Symbols";

    private const string ReplayStep = "Replaying Threads";

    private const string WatchStep = "Watching Memory";

    private const string CallTreeStep = "Building Call Tree";

    private const string OperatorsStep = "Matching Operators";

    private const string MemoryHistoryStep = "Building Memory History";

    private ILogger<TraceLoader> Logger { get; } = logger;

    private ITimeTravelRecorder? TimeTravelRecorder { get; } = timeTravelRecorder;

    public async Task PrepareAsync(DatabaseSource database, string symbolsPath, CancellationToken cancellationToken)
    {
        if (TimeTravelRecorder is not { } recorder)
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();

        var paths = await LoadedModuleReader.GetPathsAsync(database.Connection.GetConnectionString(), [.. recorder.Modules], cancellationToken);

        await ReplayFunctionResolver.PrepareAsync(paths, symbolsPath, Logger, cancellationToken);

        Logger.LogInformation("Full Trace symbols for {Modules} prepared in {Elapsed}",
                              string.Join(", ", paths.Select(Path.GetFileName)),
                              Stopwatch.GetElapsedTime(start));
    }

    public async Task<TraceResult?> LoadAsync(TraceRequest request,
                                              IProgress<ProgressDetail>? progress,
                                              CancellationToken cancellationToken)
    {
        var messages = new StepLoggerProgress(progress, Logger);

        var steps = new StepProgress(messages, GetSteps(request));

        var replayed = await ReplayAsync(request, steps, messages, progress, cancellationToken);

        if (replayed is null)
        {
            return null;
        }

        using var session = replayed.Value.Session;

        var (_, replay, functions) = replayed.Value;

        steps.Begin(CallTreeStep);

        var callTree = await BuildCallTreeAsync(replay, request.SymbolsPath, messages, cancellationToken);

        steps.Complete();

        steps.Begin(OperatorsStep);

        var builder = GetResultBuilder(replay, functions, request, callTree, session.Offsets.PlanNode);

        builder.Link();

        LogLink(request, replay, builder);

        steps.Complete();

        var memory = request.IsHeapCaptured || request.IsStackCaptured || request.IsPageReadCaptured
            ? await CaptureMemoryAsync(session,
                                       request,
                                       replay,
                                       functions,
                                       builder.Lifetimes,
                                       builder.MemoryObjects,
                                       replay.Timeline,
                                       steps,
                                       progress,
                                       cancellationToken)
            : null;

        var result = builder.Build(memory?.History ?? MemoryHistoryStore.Empty, memory?.PageReads ?? TracePageReadList.Empty);

        Logger.LogInformation("{Objects} HoBt access object(s) linked to their HoBt", result.HoBtAccess.Count);

        Logger.LogInformation("{Entries} memory map entr(ies) from allocations, constructors, member calls and declared types",
                              result.AddressMap.Count);

        Logger.LogInformation("{Sites} allocation(s) labelled with their source line, {Vtables} named by their vtable",
                              result.AddressMap.GetSiteCount(),
                              replay.AddressMap.Count(e => e is { Operation: AddressMapOperation.Allocated, ClassName: not null }));

        return result;
    }

    private async Task<(ReplaySession Session, ReplayResult Replay, ReplayFunctionTable Functions)?>
        ReplayAsync(TraceRequest request,
                    StepProgress steps,
                    StepLoggerProgress messages,
                    IProgress<ProgressDetail>? progress,
                    CancellationToken cancellationToken)
    {
        ReplaySession? session = null;

        try
        {
            steps.Begin(OpeningStep);

            session = await ReplaySession.OpenAsync(request.TraceFile, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            steps.Complete();

            steps.Begin(SymbolsStep);

            var functions = await ReplayFunctionResolver.ResolveAsync(session.Modules,
                                                                      request.SymbolsPath,
                                                                      Logger,
                                                                      messages,
                                                                      cancellationToken);

            Logger.LogInformation("{Excluded} Extended Events and tracing function(s) excluded from the replay, {Memory} memory "
                                  + "function(s) tracked",
                                  functions.ExcludedFunctions.Length,
                                  functions.AllocationFunctions.Length);

            steps.Complete();

            steps.Begin(ReplayStep);

            Logger.LogInformation("Replaying Full Trace of SQL Server {Version} for {Threads} thread(s)",
                                  session.ServerVersion,
                                  request.ThreadIds.Count);

            var replay = await session.ReplayAsync(request.ThreadIds,
                                                   functions,
                                                   request.IsStackCaptured,
                                                   steps,
                                                   cancellationToken);

            steps.Complete();

            return (session, replay, functions);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            session?.Dispose();

            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be replayed", request.TraceFile.Path);

            steps.Complete();

            progress?.Report($"Full Trace could not be replayed: {exception.Message}");

            return null;
        }
    }

    private static List<ProgressStep> GetSteps(TraceRequest request)
    {
        List<ProgressStep> steps =
        [
            new(OpeningStep, 25),
            new(SymbolsStep, 5), 
            new(ReplayStep, 50),
            new(CallTreeStep, 15),
            new(OperatorsStep, 5)
        ];

        if (request.IsHeapCaptured || request.IsPageReadCaptured)
        {
            steps.Add(new(WatchStep, 40));
        }

        if (request.IsHeapCaptured || request.IsStackCaptured || request.IsPageReadCaptured)
        {
            steps.Add(new(MemoryHistoryStep, 25));
        }

        return steps;
    }

    private async Task<CallTree> BuildCallTreeAsync(ReplayResult replay,
                                                    string symbolsPath,
                                                    StepLoggerProgress messages,
                                                    CancellationToken cancellationToken)
    {
        var (callTree, replayed) = CallTreeBuilder.Build(replay.Nodes, replay.Modules);

        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read, {UniqueLocators} unique",
                              replay.Nodes.Sum(n => (long)n.CallCount),
                              replay.Nodes.Length,
                              replay.CallStore.CallCount,
                              replay.CallStore.FunctionCount,
                              replay.CallStore.Size / (1024 * 1024),
                              replay.CallStore.LocatorCount,
                              replay.CallStore.UniqueLocatorCount);

        var frames = callTree.GetNodes().Select(node => node.Frame!).ToList();

        await SymbolDownloader.DownloadSymbols(frames.Select(f => f.Module), symbolsPath, messages, cancellationToken);

        using var resolver = new SymbolResolver(symbolsPath);

        resolver.ResolveFunctions(frames);

        LogUnknownSymbols(resolver.UnknownSymbols);

        LogUnknownEnums(callTree, resolver);

        var collapsed = new Dictionary<CallTreeNode, CallTreeNode>(ReferenceEqualityComparer.Instance);

        callTree = callTree.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallTreeNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        replay.CallStore.MapNodes(mapped);

        replay.CallStore.AnnotateNodes();

        replay.Timeline.MapNodes(mapped);

        callTree.RemoveCallsUnder(n => n.IsInstrumentation);

        replay.Timeline.RemoveCallsUnder(n => n.IsInstrumentation);

        Logger.LogInformation("{Calls} call(s) after removing Extended Events and tracing, {Spans} call span(s) on {Threads} "
                              + "thread(s) in the trace timeline",
                              callTree.GetNodes().Sum(n => n.CallCount),
                              replay.Timeline.SpanCount,
                              replay.Timeline.Threads.Count);

        return callTree;
    }

    private TraceResultBuilder GetResultBuilder(ReplayResult replay,
                                                ReplayFunctionTable functions,
                                                TraceRequest request,
                                                CallTree callTree,
                                                PlanNodeLayout planNodeLayout)
    {
        var (allocation, allocationCount, allocatedBytes, memoryObjects) = AllocationMapper.Map(replay.CallStore,
                                                                                                functions.AllocationFunctions,
                                                                                                replay.Timeline,
                                                                                                request.MemoryClerks);

        Logger.LogInformation("{Allocations} allocation(s) of {Bytes}", allocationCount, StringHelpers.FormatSize(allocatedBytes));

        return new TraceResultBuilder(callTree, replay.CallStore, replay.Timeline, request.Events, replay.PlanNodes)
        {
            Allocation = allocation,
            AddressMap = AddressMap.Build(replay.AddressMap,
                                          replay.Timeline.GetNode,
                                          replay.CallStore,
                                          functions.AllocationFunctions,
                                          memoryObjects),
            MemoryObjectSnapshot = request.MemoryClerks.MemoryObjects,
            ObjectPool = request.ObjectPool,
            BufferPool = replay.BufferPool,
            PlanNodeLayout = planNodeLayout
        };
    }

    private void LogLink(TraceRequest request, ReplayResult replay, TraceResultBuilder builder)
    {
        var operators = request.Events.OfType<ExecutionOperatorEvent>().ToList();

        Logger.LogInformation("{PlanObjects} plan object(s) linked to {PlanNodes} of {Operators} plan node(s), {Instances} iterator "
                              + "instance(s) linked to their plan node",
                              builder.PlanLink.PlanObjects.Count,
                              builder.PlanLink.PlanObjects.Values.Distinct().Count(),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              builder.PlanLink.Instances.Count);

        LogOperators(operators, replay.PlanNodes, builder.OperatorFrames, builder.Lifetimes);

        Logger.LogInformation("{Matched} of {Operators} operator(s) matched to recorded iterators, {Lifetimes} operator lifetime(s) "
                              + "on {Threads} thread(s)",
                              operators.Count(o => builder.OperatorFrames.GetInstances(o).Count > 0),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              builder.Lifetimes.Count,
                              builder.Lifetimes.Select(l => l.Thread).Distinct().Count());
    }

    private async Task<(MemoryHistoryStore History, TracePageReadList PageReads)?> CaptureMemoryAsync(ReplaySession session,
                                                                                                      TraceRequest request,
                                                                                                      ReplayResult replay,
                                                                                                      ReplayFunctionTable functions,
                                                                                                      TraceOperatorLifetimeList lifetimes,
                                                                                                      MemoryObjectMap memoryObjects,
                                                                                                      TraceTimeline timeline,
                                                                                                      StepProgress steps,
                                                                                                      IProgress<ProgressDetail>? progress,
                                                                                                      CancellationToken cancellationToken)
    {
        var allocations = request.IsHeapCaptured
            ? MemoryWatchPlanner.GetAllocations(replay.CallStore,
                                                  functions.AllocationFunctions,
                                                  replay.Buffers,
                                                  [.. memoryObjects.GetPageBlocks()])
            : [];

        var pageReads = request.IsPageReadCaptured && replay.BufferPool.Count > 0
            ? new TracePageReadList.Builder(replay.BufferPool)
            : null;

        if (allocations.Length == 0 && replay.StackSnapshotResult.Objects.Length == 0 && pageReads is null)
        {
            return null;
        }

        HashSet<ulong> iterators = [.. lifetimes.OperatorsByInstance.Keys, .. replay.PlanNodes.Select(p => p.Iterator)];

        var firstPositions = MemoryWatchPlanner.GetFirstPositions(replay.CallStore, iterators);

        var owners = AllocationOwnerResolver.GetOwners([.. allocations.Select(a => a.Block)],
                                                       replay.CallStore,
                                                       iterators,
                                                       firstPositions,
                                                       replay.PlanNodes);

        var historyBuilder = new MemoryHistoryBuilder(allocations,
                                                           owners,
                                                           firstPositions,
                                                           timeline,
                                                           replay.StackSnapshotResult,
                                                           memoryObjects.GetOwner);

        Logger.LogInformation("{Objects} stack object(s) handed between functions, read {Snapshots} time(s) into {Changes} change(s)",
                              replay.StackSnapshotResult.Objects.Length,
                              replay.StackSnapshotResult.SnapshotCount,
                              replay.StackSnapshotResult.RunCount);

        var writes = allocations.Length > 0 || pageReads is not null
            ? await WatchMemoryAsync(session, request, replay, allocations, historyBuilder, pageReads, steps, progress, cancellationToken)
            : WatchResult.Empty;

        if (writes is null)
        {
            return null;
        }

        steps.Begin(MemoryHistoryStep);

        var history = BuildMemoryHistory(historyBuilder, writes, allocations.Length);

        var reads = pageReads?.Build() ?? TracePageReadList.Empty;

        steps.Complete();

        return (history, reads);
    }

    private async Task<WatchResult?> WatchMemoryAsync(ReplaySession session,
                                                      TraceRequest request,
                                                      ReplayResult replay,
                                                      MemoryHistoryAllocation[] allocations,
                                                      MemoryHistoryBuilder historyBuilder,
                                                      TracePageReadList.Builder? pageReads,
                                                      StepProgress steps,
                                                      IProgress<ProgressDetail>? progress,
                                                      CancellationToken cancellationToken)
    {
        steps.Begin(WatchStep);

        WatchResult writes;

        try
        {
            writes = await session.WatchAsync(MemoryWatchPlanner.GetRanges(allocations.Select(a => a.Block), pageReads?.GetRanges() ?? []),
                                              GetWatchStart(allocations, pageReads),
                                              replay.Timeline.End.Position,
                                              MemoryWatchPlanner.GetWatchFilter(allocations),
                                              allocations.Length > 0 && request.CaptureHeapWrites,
                                              GetReadHandler(historyBuilder, pageReads, request.IsHeapCaptured && request.CaptureHeapReads),
                                              steps,
                                              cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogWarning(exception, "Memory could not be watched in time travel trace {TracePath}", session.TraceFile.Path);

            steps.Complete();

            progress?.Report($"Memory History could not be recorded: {exception.Message}");

            return null;
        }

        var reads = historyBuilder.ReadCounts;

        Logger.LogInformation("{Writes} of {Received} watched write(s) were to {Allocations} allocation(s) while live, "
                              + "watched in {Ranges} range(s)",
                              writes.HitCount,
                              writes.ReceivedHitCount,
                              allocations.Length,
                              writes.WatchedRangeCount);

        Logger.LogInformation("{Recorded} of {Received} watched read(s) were from live blocks, {BufferPool} from buffer pool memory, "
                              + "{Outside} outside a live block",
                              reads.CapturedCount,
                              reads.ReceivedCount,
                              reads.BufferPoolCount,
                              reads.OutsideCount);

        if (pageReads is not null)
        {
            Logger.LogInformation("{Received} read(s) of buffer pool pages kept as {Recorded} page read(s)",
                                  pageReads.ReceivedCount,
                                  pageReads.CapturedCount);
        }

        steps.Complete();

        return writes;
    }

    private static TracePosition GetWatchStart(MemoryHistoryAllocation[] allocations, TracePageReadList.Builder? pageReads)
    {
        var start = allocations.Length > 0 ? allocations.Min(a => a.Block.FromPosition) : TracePosition.Unknown;

        return pageReads?.FirstPosition is { } first && first < start ? first : start;
    }

    private static Action<WatchHit>? GetReadHandler(MemoryHistoryBuilder historyBuilder,
                                                    TracePageReadList.Builder? pageReads,
                                                    bool isHeapRead)
    {
        if (pageReads is null)
        {
            return isHeapRead ? historyBuilder.AddRead : null;
        }

        return hit =>
        {
            if (pageReads.IsFrame(hit.Address) && !historyBuilder.IsObjectMemory(hit))
            {
                pageReads.Add(hit);
            }
            else if (isHeapRead)
            {
                historyBuilder.AddRead(hit);
            }
        };
    }

    private MemoryHistoryStore BuildMemoryHistory(MemoryHistoryBuilder historyBuilder, WatchResult writes, int allocationCount)
    {
        var (history, counts) = historyBuilder.Build(writes);

        Logger.LogInformation("{Recorded} kept write(s) recorded a change, {Unchanged} left values unchanged, {BufferPool} were to "
                              + "buffer pool memory, {Outside} were outside a live block",
                              counts.CapturedCount,
                              counts.UnchangedCount,
                              counts.BufferPoolCount,
                              counts.OutsideCount);

        Logger.LogInformation("{Writes} change(s) to {Blocks} of {Allocations} watched allocation(s), {Owned} owned by an iterator",
                              history.Blocks.Where(b => !b.IsStack).Sum(b => (long)b.WriteCount),
                              history.Blocks.Count(b => !b.IsStack),
                              allocationCount,
                              history.Blocks.Count(b => b.Iterator != 0));

        Logger.LogInformation("{Writes} change(s) to {Sites} stack object(s), {Instances} call(s) of the functions holding them",
                              history.Blocks.Where(b => b.IsStack).Sum(b => (long)b.WriteCount),
                              history.Blocks.Count(b => b.IsStack),
                              history.Blocks.Sum(b => (long)b.InstanceCount));

        Logger.LogInformation("{Reads} read(s) by call recorded, {Unwritten} read(s) were from blocks with no writes",
                              history.ReadCount,
                              historyBuilder.ReadCounts.UnwrittenCount);

        return history;
    }

    private void LogOperators(IReadOnlyList<ExecutionOperatorEvent> operators,
                              IReadOnlyList<IteratorPlanNode> planNodes,
                              OperatorFrameMap frames,
                              IReadOnlyList<TraceOperatorLifetime> lifetimes)
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var planNode in planNodes)
        {
            Logger.LogDebug($"Plan node: iterator 0x{planNode.Iterator:X} node {planNode.NodeId} query 0x{planNode.Query:X}"
                            + (planNode.IsBatchHelper ? " batch helper" : string.Empty));
        }

        foreach (var operatorEvent in operators)
        {
            Logger.LogDebug($"Operator: {operatorEvent.OperatorDescription} node {operatorEvent.PlanNodeIdentifier} instances "
                            + string.Join(", ", frames.GetInstances(operatorEvent).Select(i => $"0x{i:X}")));
        }

        foreach (var lifetime in lifetimes)
        {
            Logger.LogDebug($"Operator lifetime: {lifetime.Operator.OperatorDescription} on thread {lifetime.Thread} instances "
                            + string.Join(", ", lifetime.Instances.Select(i => $"0x{i:X}")));
        }
    }

    private void LogUnknownSymbols(IReadOnlyCollection<string> unknownSymbols)
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var symbol in unknownSymbols.Order(StringComparer.Ordinal))
        {
            Logger.LogDebug($"Unknown symbol: {symbol}");
        }
    }

    private void LogUnknownEnums(CallTree callTree, SymbolResolver resolver)
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var functions = new HashSet<string>(StringComparer.Ordinal);

        var unknown = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var node in callTree.GetNodes())
        {
            if (node.Frame is not { } frame)
            {
                continue;
            }

            var symbol = frame.QualifiedName;

            if (!functions.Add(symbol)
                || resolver.ResolveSymbol(frame)?.Parsed is not { } signature)
            {
                continue;
            }

            var types = signature.ReturnType is { } returnType ? signature.Parameters.Append(returnType) : signature.Parameters;

            foreach (var type in types)
            {
                if (ArgumentValue.GetUnknownEnum(type) is { } name)
                {
                    unknown.TryAdd(name, symbol);
                }
            }
        }

        foreach (var (name, function) in unknown)
        {
            Logger.LogDebug($"Unknown enum: {name} ({function})");
        }
    }
}
