using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Capture;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Output;
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

        var paths = await LoadedModules.PathsOfAsync(database.Connection.GetConnectionString(), [.. recorder.Modules], cancellationToken);

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

        var (callStack, mapped) = await BuildCallStackAsync(replay, request.SymbolsPath, messages, cancellationToken);

        var timeline = BuildTimeline(replay, callStack, mapped);

        steps.Complete();

        steps.Begin(OperatorsStep);

        var result = BuildResult(replay, functions, request, callStack, timeline);

        steps.Complete();

        if ((request.IsHeapCaptured || request.IsStackCaptured || request.IsPageReadCaptured)
            && await RecordMemoryAsync(session,
                                       request,
                                       replay,
                                       functions,
                                       result.Lifetimes,
                                       result.MemoryObjects,
                                       timeline,
                                       steps,
                                       progress,
                                       cancellationToken) is { } memory)
        {
            result = result with { MemoryHistory = memory.History, PageReads = memory.PageReads };
        }

        result = result.WithPageAccesses();

        Logger.LogInformation("{Objects} HoBt access object(s) linked to their HoBt", result.HoBtAccess.Count);

        Logger.LogInformation("{Entries} memory map entr(ies) from allocations, constructors, member calls and declared types",
                              result.AddressMap.Count);

        Logger.LogInformation("{Sites} allocation(s) labelled with their source line, {Vtables} named by their vtable",
                              result.AddressMap.GetSiteCount(),
                              replay.AddressMap.Count(e => e is { Operation: AddressMapOperation.Allocated, ClassName: not null }));

        result.CallStore.ReleaseColumns();

        LogComponentSizes(result);

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

    private async Task<(CallStackTree CallStack, CallStackNode?[] Mapped)> BuildCallStackAsync(ReplayResult replay,
                                                                                               string symbolsPath,
                                                                                               StepLoggerProgress messages,
                                                                                               CancellationToken cancellationToken)
    {
        var (callStack, replayed) = CallTreeBuilder.Build(replay.Nodes, replay.Modules);

        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read, {UniqueLocators} unique",
                              replay.Nodes.Sum(n => (long)n.Calls),
                              replay.Nodes.Length,
                              replay.CallStore.Calls,
                              replay.CallStore.FunctionCount,
                              replay.CallStore.Size / (1024 * 1024),
                              replay.CallStore.LocatorCount,
                              replay.CallStore.UniqueLocatorCount);

        var unknownSymbols = await CallstackProcessor.Process(callStack, symbolsPath, messages, cancellationToken);

        LogUnknownSymbols(unknownSymbols);

        LogUnknownEnums(callStack, symbolsPath);

        var collapsed = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        callStack = callStack.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallStackNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        return (callStack, mapped);
    }

    private void LogComponentSizes(TraceResult result)
    {
        long measured = 0;

        foreach (var (component, items, bytes) in result.GetComponentSizes())
        {
            if (bytes is { } size)
            {
                measured += size;

                Logger.LogInformation("{Component}: {Items:N0} in {Size:N1} MB", component, items, size / (1024d * 1024));
            }
            else
            {
                Logger.LogInformation("{Component}: {Items:N0}", component, items);
            }
        }

        Logger.LogInformation("Measured components: {Size:N1} MB", measured / (1024d * 1024));
    }

    private TraceTimeline BuildTimeline(ReplayResult replay, CallStackTree callStack, CallStackNode?[] mapped)
    {
        replay.CallStore.MapNodes(mapped);

        replay.CallStore.AnnotateNodes();

        replay.Timeline.MapNodes(mapped);

        callStack.RemoveCallsUnder(n => n.IsInstrumentation);

        replay.Timeline.RemoveCallsUnder(n => n.IsInstrumentation);

        var timeline = replay.Timeline;

        Logger.LogInformation("{Calls} call(s) after removing Extended Events and tracing, {Spans} call span(s) on {Threads} "
                              + "thread(s) in the trace timeline",
                              callStack.Nodes().Sum(n => n.Calls),
                              timeline.SpanCount,
                              timeline.Threads.Count);

        return timeline;
    }

    private TraceResult BuildResult(ReplayResult replay,
                                    ReplayFunctionTable functions,
                                    TraceRequest request,
                                    CallStackTree callStack,
                                    TraceTimeline timeline)
    {
        var events = request.Events;

        var operators = events.OfType<ExecutionOperatorEvent>().ToList();

        var (memory, allocationCount, allocatedBytes, memoryObjects) = AllocationMapper.Map(replay.CallStore,
                                                                                            functions.AllocationFunctions,
                                                                                            timeline,
                                                                                            request.MemoryClerks);

        var getAllocation = GetAllocation(replay.CallStore, functions.AllocationFunctions, memoryObjects);

        var builder = new TraceResultBuilder(callStack, replay.CallStore, timeline, events, replay.PlanNodes)
        {
            Allocation = memory,
            AddressMap = AddressMap.Build(replay.AddressMap, timeline.NodeOf, getAllocation),
            MemoryObjectSnapshot = request.MemoryClerks.MemoryObjects,
            ObjectPool = request.ObjectPool,
            BufferPool = replay.BufferPool
        };

        var matched = IteratorInstanceMatcher.Match(callStack, events, builder.PlanLink);

        OperatorEntryLabels.Apply(events);

        Logger.LogInformation("{PlanObjects} plan object(s) linked to {PlanNodes} of {Operators} plan node(s), {Instances} iterator "
                              + "instance(s) linked to their plan node",
                              builder.PlanLink.PlanObjects.Count,
                              builder.PlanLink.PlanObjects.Values.Distinct().Count(),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              builder.PlanLink.Instances.Count);

        var lifetimes = builder.BuildLifetimes();

        LogOperators(operators, replay.PlanNodes, lifetimes);

        var outputStructures = ResolveOutputStructures(replay, memory, lifetimes);

        Logger.LogInformation("{Matched} of {Operators} operator(s) matched to recorded iterators, {Allocations} allocation(s) of "
                              + "{Bytes}, {Lifetimes} operator lifetime(s) on {Threads} thread(s)",
                              matched,
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              allocationCount,
                              StringHelpers.FormatSize(allocatedBytes),
                              lifetimes.Count,
                              lifetimes.Select(l => l.Thread).Distinct().Count());

        if (events.Count > 0)
        {
            callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), QueryRunner.ActivityBuckets);
        }

        return builder.Build(outputStructures);
    }

    private async Task<(MemoryHistoryStore History, TracePageReadList PageReads)?> RecordMemoryAsync(ReplaySession session,
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
            ? MemoryHistoryBuilder.GetAllocations(replay.CallStore,
                                                  functions.AllocationFunctions,
                                                  replay.Buffers,
                                                  [.. memoryObjects.GetPageBlocks()])
            : [];

        var pageReads = request.IsPageReadCaptured && replay.BufferPool.Count > 0
            ? new TracePageReadRecorder(replay.BufferPool)
            : null;

        if (allocations.Length == 0 && replay.StackSnapshotResult.Objects.Length == 0 && pageReads is null)
        {
            return null;
        }

        HashSet<ulong> iterators = [.. lifetimes.OperatorsByInstance.Keys, .. replay.PlanNodes.Select(p => p.Iterator)];

        var owners = IteratorAllocationIndex.GetOwners([.. allocations.Select(a => a.Block)], replay.CallStore, iterators, replay.PlanNodes);

        var recording = new MemoryHistoryBuilder.Recording(allocations,
                                                           owners,
                                                           MemoryHistoryBuilder.GetFirstPositions(replay.CallStore, iterators),
                                                           timeline,
                                                           replay.StackSnapshotResult);

        Logger.LogInformation("{Objects} stack object(s) handed between functions, read {Snapshots} time(s) into {Changes} change(s)",
                              replay.StackSnapshotResult.Objects.Length,
                              replay.StackSnapshotResult.SnapshotCount,
                              replay.StackSnapshotResult.RunCount);

        var writes = allocations.Length > 0 || pageReads is not null
            ? await WatchMemoryAsync(session, request, replay, allocations, recording, pageReads, steps, progress, cancellationToken)
            : WatchResult.Empty;

        if (writes is null)
        {
            return null;
        }

        steps.Begin(MemoryHistoryStep);

        var history = BuildMemoryHistory(recording, writes, allocations.Length);

        var reads = pageReads?.Build() ?? TracePageReadList.Empty;

        steps.Complete();

        return (history, reads);
    }

    private async Task<WatchResult?> WatchMemoryAsync(ReplaySession session,
                                                      TraceRequest request,
                                                      ReplayResult replay,
                                                      MemoryHistoryAllocation[] allocations,
                                                      MemoryHistoryBuilder.Recording recording,
                                                      TracePageReadRecorder? pageReads,
                                                      StepProgress steps,
                                                      IProgress<ProgressDetail>? progress,
                                                      CancellationToken cancellationToken)
    {
        steps.Begin(WatchStep);

        WatchResult writes;

        try
        {
            writes = await session.WatchAsync(MemoryHistoryBuilder.GetRanges(allocations.Select(a => a.Block), pageReads?.GetRanges() ?? []),
                                              GetWatchStart(allocations, pageReads),
                                              replay.Timeline.End.Position,
                                              MemoryHistoryBuilder.GetWatchFilter(allocations),
                                              allocations.Length > 0 && request.CaptureHeapWrites,
                                              GetReadHandler(recording, pageReads, request.IsHeapCaptured && request.CaptureHeapReads),
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

        var reads = recording.ReadCounts;

        Logger.LogInformation("{Writes} of {Received} watched write(s) were to {Allocations} allocation(s) while live, "
                              + "watched in {Ranges} range(s)",
                              writes.HitCount,
                              writes.ReceivedHitCount,
                              allocations.Length,
                              writes.WatchedRanges);

        Logger.LogInformation("{Recorded} of {Received} watched read(s) were from live blocks, {BufferPool} from buffer pool memory, "
                              + "{Outside} outside a live block",
                              reads.Recorded,
                              reads.Received,
                              reads.BufferPool,
                              reads.Outside);

        if (pageReads is not null)
        {
            Logger.LogInformation("{Received} read(s) of buffer pool pages kept as {Recorded} page read(s)",
                                  pageReads.Received,
                                  pageReads.Recorded);
        }

        steps.Complete();

        return writes;
    }

    private static TracePosition GetWatchStart(MemoryHistoryAllocation[] allocations, TracePageReadRecorder? pageReads)
    {
        var start = allocations.Length > 0 ? allocations.Min(a => a.Block.Start) : TracePosition.Unknown;

        return pageReads?.FirstPosition is { } first && first < start ? first : start;
    }

    private static Action<WatchHit>? GetReadHandler(MemoryHistoryBuilder.Recording recording,
                                                    TracePageReadRecorder? pageReads,
                                                    bool isHeapRead)
    {
        if (pageReads is null)
        {
            return isHeapRead ? recording.AddRead : null;
        }

        return hit =>
        {
            if (pageReads.IsFrame(hit.Address) && !recording.IsObjectMemory(hit))
            {
                pageReads.Add(hit);
            }
            else if (isHeapRead)
            {
                recording.AddRead(hit);
            }
        };
    }

    private MemoryHistoryStore BuildMemoryHistory(MemoryHistoryBuilder.Recording recording, WatchResult writes, int allocationCount)
    {
        var (history, counts) = recording.Build(writes);

        Logger.LogInformation("{Recorded} kept write(s) recorded a change, {Unchanged} left values unchanged, {BufferPool} were to "
                              + "buffer pool memory, {Outside} were outside a live block",
                              counts.Recorded,
                              counts.Unchanged,
                              counts.BufferPool,
                              counts.Outside);

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
                              recording.ReadCounts.Unwritten);

        return history;
    }

    private List<TraceOutputStructure> ResolveOutputStructures(ReplayResult replay, AllocationIndex allocation, TraceOperatorLifetimeList lifetimes)
    {
        var allocations = IteratorAllocationIndex.Build(allocation,
                                                    replay.CallStore,
                                                    lifetimes.OperatorsByInstance.Keys.ToHashSet(),
                                                    replay.PlanNodes);

        List<TraceOutputStructure> structures = [.. replay.OutputAddresses.Select(a => TraceOutputStructure.Resolve(a, allocations))];

        Logger.LogInformation("{Structures} output structure(s) resolved through {Blocks} allocation(s), "
                              + "{Owned} of {Columns} column(s) owned",
                              structures.Count,
                              allocations.Count,
                              structures.Sum(s => s.Owners.Count(o => o != 0)),
                              structures.Sum(s => s.Owners.Count));

        return structures;
    }

    private void LogOperators(IReadOnlyList<ExecutionOperatorEvent> operators,
                              IReadOnlyList<IteratorPlanNode> planNodes,
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
                            + string.Join(", ", operatorEvent.Instances.Select(i => $"0x{i:X}")));
        }

        foreach (var lifetime in lifetimes)
        {
            Logger.LogDebug($"Operator lifetime: {lifetime.Operator.OperatorDescription} on thread {lifetime.Thread} instances "
                            + string.Join(", ", lifetime.Operator.Instances.Select(i => $"0x{i:X}")));
        }
    }

    private void LogUnknownSymbols(string[] unknownSymbols)
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

    private void LogUnknownEnums(CallStackTree callStack, string symbolsPath)
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        using var resolver = new CallstackResolver(symbolsPath);

        var functions = new HashSet<string>(StringComparer.Ordinal);

        var unknown = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var node in callStack.Nodes())
        {
            if (node.Frame is not { } frame)
            {
                continue;
            }

            var symbol = QualifiedSymbol.Of(frame);

            if (!functions.Add(symbol)
                || FunctionSignature.Parse(resolver.ResolveSignature(frame), resolver.ResolveDecoratedName(frame)) is not { } signature)
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

    private static Func<AddressEntry, CallStackNode?, AddressMapAllocation> GetAllocation(TraceCallStore store,
                                                                                          IReadOnlyList<AllocationFunction> functions,
                                                                                          AllocationMemoryObjectLookup memoryObjects)
    {
        var allocators = new Dictionary<ulong, AllocationFunction>();

        foreach (var function in functions)
        {
            allocators.TryAdd(function.Address, function);
        }

        return (entry, node) =>
        {
            if (node?.Frame is not { } frame
                || !allocators.TryGetValue(frame.Address, out var allocator)
                || store.GetCallList(frame.Address, frame.InstanceAddress) is not { } calls
                || entry.CallIndex < 0
                || entry.CallIndex >= calls.Count)
            {
                return default;
            }

            var call = calls[entry.CallIndex];

            return new AddressMapAllocation(allocator.GetByteCount(call),
                                           allocator.GetSite(call),
                                           memoryObjects.GetMemoryObject(frame.Address, entry.CallIndex));
        };
    }
}
