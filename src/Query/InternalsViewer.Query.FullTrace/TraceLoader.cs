using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Replay.Native;
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
using InternalsViewer.Query.FullTrace.Replay.Functions;
using InternalsViewer.Query.FullTrace.Replay;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Runs the trace loading process
/// </summary>
/// <remarks>
/// Steps to run a trace:
///
/// 1. Trace is opened (and indexed) using the TimeTravelBridge C++ library
///
/// 2. Symbols are resolved using ReplayFunctionResolver
///
/// 3. Trace threads replayed:
/// 
///     Call chunks
///     -----------
///
///     Calls are logged in chunks where each call is recorded vertically as a set of 11 ulong values in a single dimensional ulong array.
///
///     The bridge builds up the chunks and periodically flushes them.
///
///     Calls record the execution of a function on a thread including:
/// 
///     - Start position (sequence/steps)
///     - Thread Id + Bit 32 return flag
///     - Register arguments RCX, RDX, R8, and R9
///     - Call path node index
///     - End position (sequence/steps)
///
///     Node Index is generated inside the bridge and is the key that links the other data structures.
///
///     Spans
///     -----
///
///     
///     
///    
/// </remarks>
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
        if (TimeTravelRecorder is null)
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();

        var paths = await LoadedModuleReader.GetPathsAsync(database.Connection.GetConnectionString(), 
                                                           [.. TimeTravelRecorder.Modules], 
                                                           cancellationToken);

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

        if (!request.IsHeapCaptured && !request.IsPageReadCaptured)
        {
            session.Dispose();
        }

        var (builder, memory) = await TranslateAsync(replayed.Value.Capture,
                                                     replayed.Value.Functions,
                                                     request,
                                                     session.ServerVersion,
                                                     steps,
                                                     messages,
                                                     cancellationToken);

        var (history, pageReads) = memory is null
            ? (MemoryHistoryStore.Empty, TracePageReadList.Empty)
            : await CaptureMemoryAsync(session, request, builder.Store.Timeline, memory, steps, progress, cancellationToken);

        await Task.Yield();

        var result = builder.Build(history, pageReads);

        Logger.LogInformation("{Objects} HoBt access object(s) linked to their HoBt", result.HoBtAccess.Count);

        return result;
    }

    private async Task<(ReplaySession Session, ReplayCapture Capture, ReplayFunctionTable Functions)?>
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

    private async Task<(TraceResultBuilder Builder, MemoryCapture? Memory)> TranslateAsync(ReplayCapture capture,
                                                                                         ReplayFunctionTable functions,
                                                                                         TraceRequest request,
                                                                                         Version? serverVersion,
                                                                                         StepProgress steps,
                                                                                         StepLoggerProgress messages,
                                                                                         CancellationToken cancellationToken)
    {
        var bufferPool = TraceBufferPool.Build(capture.BufferPool);

        steps.Begin(CallTreeStep);

        var (callTree, nodes) = await BuildCallTreeAsync(capture, request.SymbolsPath, messages, cancellationToken);

        var store = TraceStore.Build(capture, nodes);

        LogCalls(capture, store);

        steps.Complete();

        steps.Begin(OperatorsStep);

        var builder = CreateResultBuilder(capture, store, bufferPool, functions, request, callTree, serverVersion);

        builder.LinkExecutionPlan();

        LogExecutionPlanLink(request, capture, builder);

        steps.Complete();

        var memory = request.IsHeapCaptured || request.IsStackCaptured || request.IsPageReadCaptured
            ? PrepareMemoryCapture(request, capture, functions, builder)
            : null;

        return (builder, memory);
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

    private async Task<(CallTree Tree, TraceNodeMap Nodes)> BuildCallTreeAsync(ReplayCapture capture,
                                                                              string symbolsPath,
                                                                              StepLoggerProgress messages,
                                                                              CancellationToken cancellationToken)
    {
        var (callTree, nodes) = CallTreeBuilder.Build(capture.CallNodes, capture.Modules);

        var frames = callTree.GetNodes().Select(node => node.Frame!).ToList();

        List<CallFrame> sqlServerFrames = [.. frames.Where(f => f.Module.IsSqlServer)];

        await SymbolDownloader.DownloadSymbols(sqlServerFrames.Select(f => f.Module), symbolsPath, messages, cancellationToken);

        using var resolver = new SymbolResolver(symbolsPath);

        resolver.ResolveFunctions(sqlServerFrames);

        SymbolResolver.ResolveExports(frames.Where(f => !f.Module.IsSqlServer), capture.Modules);

        LogUnknownSymbols(resolver.UnknownSymbols);

        LogUnknownEnums(callTree, resolver);

        return (callTree, nodes);
    }

    private void LogCalls(ReplayCapture capture, TraceStore store)
    {
        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read, {UniqueLocators} unique",
                              capture.CallNodes.Sum(n => (long)n.CallCount),
                              capture.CallNodes.Length,
                              store.Calls.CallCount,
                              store.Calls.FunctionCount,
                              store.Calls.CompressedSize / (1024 * 1024),
                              store.Calls.Locators.Count,
                              store.Calls.Locators.ValueCount);

        Logger.LogInformation("{Spans} call span(s) on {Threads} thread(s) in the trace timeline",
                              store.Timeline.SpanCount,
                              store.Timeline.Threads.Count);
    }

    private TraceResultBuilder CreateResultBuilder(ReplayCapture capture,
                                                TraceStore store,
                                                TraceBufferPool bufferPool,
                                                ReplayFunctionTable functions,
                                                TraceRequest request,
                                                CallTree callTree,
                                                Version? serverVersion)
    {
        var (allocation, allocationCount, allocatedBytes, memoryObjects) = AllocationMapper.Map(store.Calls,
                                                                                                functions.AllocationFunctions,
                                                                                                store.Timeline,
                                                                                                request.MemoryClerks);

        Logger.LogInformation("{Allocations} allocation(s) of {Bytes}", allocationCount, StringHelpers.FormatSize(allocatedBytes));

        var addressMap = AddressMap.Build(capture.AddressEntries,
                                          store.Nodes.GetNode,
                                          store.Calls,
                                          functions.AllocationFunctions,
                                          memoryObjects);

        Logger.LogInformation("{Entries} memory map entr(ies) from allocations, constructors, member calls and declared types",
                              addressMap.Count);

        Logger.LogInformation("{Sites} allocation(s) labelled with their source line, {Vtables} named by their vtable",
                              addressMap.GetSiteCount(),
                              capture.AddressEntries.Count(e => e is { Operation: AddressMapOperation.Allocated, ClassName: not null }));

        return new TraceResultBuilder(callTree, store, request.Events, capture.PlanNodes)
        {
            Allocation = allocation,
            AddressMap = addressMap,
            MemoryClerkObjects = request.MemoryClerks.MemoryObjects,
            BufferPool = bufferPool,
            ServerVersion = serverVersion,
            SymbolsPath = request.SymbolsPath,
            ModuleImages = capture.Modules
        };
    }

    private void LogExecutionPlanLink(TraceRequest request, ReplayCapture capture, TraceResultBuilder builder)
    {
        var operators = request.Events.OfType<ExecutionOperatorEvent>().ToList();

        Logger.LogInformation("{PlanObjects} plan object(s) linked to {PlanNodes} of {Operators} plan node(s), {Instances} iterator "
                              + "instance(s) linked to their plan node",
                              builder.PlanLink.PlanObjects.Count,
                              builder.PlanLink.PlanObjects.Values.Distinct().Count(),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              builder.PlanLink.Instances.Count);

        LogOperators(operators, capture.PlanNodes, builder.OperatorFrames, builder.Lifetimes);

        Logger.LogInformation("{Matched} of {Operators} operator(s) matched to recorded iterators, {Lifetimes} operator lifetime(s) "
                              + "on {Threads} thread(s)",
                              operators.Count(o => builder.OperatorFrames.GetInstances(o).Count > 0),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              builder.Lifetimes.Count,
                              builder.Lifetimes.Select(l => l.Thread).Distinct().Count());
    }

    private MemoryCapture? PrepareMemoryCapture(TraceRequest request,
                                                ReplayCapture capture,
                                                ReplayFunctionTable functions,
                                                TraceResultBuilder builder)
    {
        var store = builder.Store;

        var allocations = request.IsHeapCaptured
            ? MemoryWatchPlanner.GetAllocations(store.Calls,
                                                  functions.AllocationFunctions,
                                                  capture.BufferBlocks,
                                                  [.. builder.MemoryObjects.GetPageBlocks()])
            : [];

        var pageReads = request.IsPageReadCaptured && builder.BufferPool.Count > 0
            ? new TracePageReadList.Builder(builder.BufferPool)
            : null;

        if (allocations.Length == 0 && capture.StackSnapshots.Objects.Length == 0 && pageReads is null)
        {
            return null;
        }

        HashSet<ulong> iterators = [.. builder.Lifetimes.OperatorsByInstance.Keys, .. capture.PlanNodes.Select(p => p.Iterator)];

        var firstPositions = MemoryWatchPlanner.GetFirstPositions(store.Calls, iterators);

        var owners = AllocationOwnerResolver.GetOwners([.. allocations.Select(a => a.Block)],
                                                       store.Calls,
                                                       iterators,
                                                       firstPositions,
                                                       capture.PlanNodes);

        var historyBuilder = new MemoryHistoryBuilder(allocations,
                                                           owners,
                                                           firstPositions,
                                                           store.Timeline,
                                                           capture.StackSnapshots,
                                                           builder.MemoryObjects.GetOwner);

        Logger.LogInformation("{Objects} stack object(s) handed between functions, read {Snapshots} time(s) into {Changes} change(s)",
                              capture.StackSnapshots.Objects.Length,
                              capture.StackSnapshots.SnapshotCount,
                              capture.StackSnapshots.RunCount);

        return new MemoryCapture(allocations, historyBuilder, pageReads);
    }

    private async Task<(MemoryHistoryStore History, TracePageReadList PageReads)> CaptureMemoryAsync(ReplaySession session,
                                                                                                     TraceRequest request,
                                                                                                     TraceTimeline timeline,
                                                                                                     MemoryCapture memory,
                                                                                                     StepProgress steps,
                                                                                                     IProgress<ProgressDetail>? progress,
                                                                                                     CancellationToken cancellationToken)
    {
        var writes = memory.IsWatched
            ? await WatchMemoryAsync(session, request, timeline, memory, steps, progress, cancellationToken)
            : WatchResult.Empty;

        session.Dispose();

        return writes is null
            ? (MemoryHistoryStore.Empty, TracePageReadList.Empty)
            : BuildMemoryHistory(memory, writes, steps);
    }

    private async Task<WatchResult?> WatchMemoryAsync(ReplaySession session,
                                                      TraceRequest request,
                                                      TraceTimeline timeline,
                                                      MemoryCapture memory,
                                                      StepProgress steps,
                                                      IProgress<ProgressDetail>? progress,
                                                      CancellationToken cancellationToken)
    {
        var (allocations, historyBuilder, pageReads) = memory;

        steps.Begin(WatchStep);

        WatchResult writes;

        try
        {
            writes = await session.WatchAsync(MemoryWatchPlanner.GetRanges(allocations.Select(a => a.Block), pageReads?.GetRanges() ?? []),
                                              GetWatchStart(allocations, pageReads),
                                              timeline.End.Position,
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

    private (MemoryHistoryStore History, TracePageReadList PageReads) BuildMemoryHistory(MemoryCapture memory,
                                                                                         WatchResult writes,
                                                                                         StepProgress steps)
    {
        var (allocations, historyBuilder, pageReads) = memory;

        steps.Begin(MemoryHistoryStep);

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
                              allocations.Length,
                              history.Blocks.Count(b => b.Iterator != 0));

        Logger.LogInformation("{Writes} change(s) to {Sites} stack object(s), {Instances} call(s) of the functions holding them",
                              history.Blocks.Where(b => b.IsStack).Sum(b => (long)b.WriteCount),
                              history.Blocks.Count(b => b.IsStack),
                              history.Blocks.Sum(b => (long)b.InstanceCount));

        Logger.LogInformation("{Reads} read(s) by call recorded, {Unwritten} read(s) were from blocks with no writes",
                              history.ReadCount,
                              historyBuilder.ReadCounts.UnwrittenCount);

        var reads = pageReads?.Build() ?? TracePageReadList.Empty;

        steps.Complete();

        return (history, reads);
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

    private sealed record MemoryCapture(MemoryHistoryAllocation[] Allocations,
                                        MemoryHistoryBuilder History,
                                        TracePageReadList.Builder? PageReads)
    {
        public bool IsWatched => Allocations.Length > 0 || PageReads is not null;
    }
}
