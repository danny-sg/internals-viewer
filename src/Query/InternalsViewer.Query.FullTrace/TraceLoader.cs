using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.FullTrace.Memory;
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

    private const string CallTreeStep = "Building Call Tree";

    private const string OperatorsStep = "Matching Operators";

    private static readonly ProgressStep[] Steps =
    [
        new(OpeningStep, 25),
        new(SymbolsStep, 5),
        new(ReplayStep, 50),
        new(CallTreeStep, 15),
        new(OperatorsStep, 5)
    ];

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
                                              IReadOnlyList<CustomHookDefinition> customHooks,
                                              IProgress<ProgressDetail>? progress,
                                              CancellationToken cancellationToken)
    {
        var events = request.Events;

        var operators = events.OfType<ExecutionOperatorEvent>().ToList();

        var messages = new StepLoggerProgress(progress, Logger);

        var steps = new StepProgress(messages, Steps);

        var details = new LoggerProgress(Logger);

        ReplayResult replay;

        ReplayFunctions functions;

        try
        {
            steps.Begin(OpeningStep);

            using var session = await ReplaySession.OpenAsync(request.TraceFile, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            steps.Complete();

            steps.Begin(SymbolsStep);

            functions = await ReplayFunctionResolver.ResolveAsync(session.Modules,
                                                                  request.SymbolsPath,
                                                                  Logger,
                                                                  messages,
                                                                  cancellationToken);

            Logger.LogInformation("{Excluded} Extended Events and tracing function(s) excluded from the replay, {Memory} memory "
                                  + "function(s) tracked",
                                  functions.ExcludedFunctions.Length,
                                  functions.MemoryFunctions.Length);

            steps.Complete();

            steps.Begin(ReplayStep);

            Logger.LogInformation("Replaying Full Trace for {Threads} thread(s)", request.ThreadIds.Count);

            replay = await session.ReplayAsync(request.ThreadIds, functions, customHooks, steps, cancellationToken);

            steps.Complete();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be replayed", request.TraceFile.Path);

            steps.Complete();

            progress?.Report($"Full Trace could not be replayed: {exception.Message}");

            return null;
        }

        steps.Begin(CallTreeStep);

        var (callStack, replayed) = CallTreeBuilder.Build(replay.Nodes, replay.Modules);

        var nodes = replay.Nodes;

        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read",
                              nodes.Sum(n => (long)n.Calls),
                              nodes.Length,
                              replay.CallStore.Calls,
                              replay.CallStore.FunctionCount,
                              replay.CallStore.Size / (1024 * 1024),
                              replay.CallStore.LocatorCount);

        var unknownSymbols = await CallstackProcessor.Process(callStack, request.SymbolsPath, messages, cancellationToken);

        LogUnknownSymbols(unknownSymbols);

        LogUnknownEnums(callStack, request.SymbolsPath);

        var collapsed = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        callStack = callStack.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallStackNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        var planLink = IteratorPlanLinker.Link(replay.CallStore, mapped, events);

        var hobtAccess = HoBtAccessMap.Build(replay.CallStore, mapped);

        replay.CallStore.MapNodes(mapped);

        replay.CallStore.AnnotateNodes();

        replay.RowBuffers.AnnotateCallStackNodes(replay.CallStore);

        replay.Batches.AnnotateNodes(replay.CallStore);

        replay.Timeline.MapNodes(mapped);

        replay.Timeline.SetCalls(replay.CallStore);

        callStack.RemoveCallsUnder(IsInstrumentation);

        var timeline = replay.Timeline.WithoutCallsUnder(IsInstrumentation);

        Logger.LogInformation("{Calls} call(s) after removing Extended Events and tracing, {Spans} call span(s) on {Threads} "
                              + "thread(s) in the flame chart",
                              callStack.Nodes().Sum(n => n.Calls),
                              timeline.SpanCount,
                              timeline.Threads.Count);

        var memoryMap = MemoryMap.Build(replay.MemoryMap, replay.Timeline.NodeOf);

        steps.Complete();

        steps.Begin(OperatorsStep);

        var matched = IteratorInstanceMatcher.Match(callStack, events, planLink);

        OperatorEntryLabels.Apply(events);

        Logger.LogInformation("{PlanObjects} plan object(s) linked to {PlanNodes} of {Operators} plan node(s), {Instances} iterator "
                              + "instance(s) linked to their plan node",
                              planLink.PlanObjects.Count,
                              planLink.PlanObjects.Values.Distinct().Count(),
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              planLink.Instances.Count);

        var memory = AllocationMapper.Map(replay.CallStore, functions.MemoryFunctions, timeline, request.MemoryClerks);

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, operators);

        timeline.SetLifetimes(lifetimes);

        var expanded = events.ExpandOwned();

        var linked = PublishedEventLinker.Link(replay.PublishedEvents,
                                               functions.PublishedEvents.Publishers,
                                               request.RawEvents,
                                               expanded,
                                               timeline);

        timeline.SetEventLinks(linked.Links);

        Logger.LogInformation("{Linked} of {Events} event(s) linked to their trace position: {Written} of {Published} published "
                              + "event(s) written to the session, {Paired} paired with an event",
                              timeline.EventLinks.Count,
                              request.RawEvents.Count,
                              linked.Written,
                              linked.Published,
                              linked.Paired);

        Logger.LogInformation("{Matched} of {Operators} operator(s) matched to recorded iterators, {Allocations} allocation(s) of "
                              + "{Bytes}, {Lifetimes} operator lifetime(s) on {Threads} thread(s)",
                              matched,
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              memory.Allocations,
                              Size(memory.Bytes),
                              lifetimes.Count,
                              lifetimes.Select(l => l.Thread).Distinct().Count());

        if (events.Count > 0)
        {
            callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), QueryRunner.ActivityBuckets);
        }

        steps.Complete();

        Logger.LogInformation("{Objects} HoBt access object(s) linked to their HoBt", hobtAccess.Count);

        Logger.LogInformation("{Entries} memory map entr(ies) from allocations, constructors and member calls", memoryMap.Count);

        Logger.LogInformation("{Rows} row buffer(s) read from iterator GetRow calls", replay.RowBuffers.Count);

        Logger.LogInformation("{Batches} batch(es) read from iterator BpGetBatchData calls", replay.Batches.Count);

        Logger.LogInformation("{Captures} custom hook capture(s) read from {Hooks} hook(s)", replay.CustomHooks.Count, customHooks.Count);

        return new TraceResult(callStack, replay.CallStore, timeline)
        {
            PlanLink = planLink,
            HoBtAccess = hobtAccess,
            MemoryMap = memoryMap,
            ObjectPool = request.ObjectPool,
            RowBuffers = replay.RowBuffers,
            Batches = replay.Batches,
            CustomHooks = replay.CustomHooks,
            OutputStructures = replay.OutputStructures,
            RowBufferMarks = TraceRowBufferMarks.Build(timeline, replay.RowBuffers, replay.Batches),
            RowReturnMarks = TraceRowReturnMarks.Build(timeline,
                                                       replay.CallStore,
                                                       TraceOutputLineage.Build(timeline, replay.OutputStructures, replay.RowBuffers))
        };
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

    private static bool IsInstrumentation(CallStackNode node) => node.IsExtendedEvents || (node.IsTracing && !node.HasOperator);

    private static string Size(ulong bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):N1} MB" : $"{bytes / 1024.0:N1} KB";

    private void DeleteTimeTravelTrace(TimeTravelTraceFile trace, IProgress<ProgressDetail>? progress)
    {
        try
        {
            var size = new FileInfo(trace.Path).Length;

            File.Delete(trace.Path);

            progress?.Report($"Time travel trace deleted ({size / (1024.0 * 1024.0):N0} MB)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be deleted", trace.Path);
        }
    }
}
