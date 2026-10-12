using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Markers;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.Helpers;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace;

public sealed class FullTraceLoader(ILogger<FullTraceLoader> logger, ITimeTravelRecorder? timeTravelRecorder = null)
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

    private ILogger<FullTraceLoader> Logger { get; } = logger;

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

    public async Task<FullTraceResult?> LoadAsync(FullTraceRequest request,
                                                  IProgress<ProgressDetail>? progress,
                                                  CancellationToken cancellationToken)
    {
        var events = request.Events;

        var operators = events.OfType<ExecutionOperatorEvent>().ToList();

        var messages = new StepLoggerProgress(progress, Logger);

        var steps = new StepProgress(messages, Steps);

        var details = new LoggerProgress(Logger);

        ReplayResult replay;

        ReplayFunctionSet functions;

        IReadOnlyList<MemoryFunction> memoryFunctions;

        try
        {
            steps.Begin(OpeningStep);

            using var session = await ReplaySession.OpenAsync(request.TraceFile, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            steps.Complete();

            steps.Begin(SymbolsStep);

            (functions, memoryFunctions) = await ReplayFunctionResolver.ResolveAsync(session.Modules,
                                                                                     request.SymbolsPath,
                                                                                     Logger,
                                                                                     messages,
                                                                                     cancellationToken);

            Logger.LogInformation("{Excluded} Extended Events and tracing function(s) excluded from the replay, {Memory} memory "
                                  + "function(s) tracked",
                                  functions.Excluded.Length,
                                  memoryFunctions.Count);

            steps.Complete();

            steps.Begin(ReplayStep);

            Logger.LogInformation("Replaying Full Trace for {Threads} thread(s)", request.ThreadIds.Count);

            replay = await session.ReplayAsync(request.ThreadIds, functions, steps, cancellationToken);

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

        DeleteTimeTravelTrace(request.TraceFile, details);

        var (callStack, replayed) = CallTreeBuilder.Build(replay.Nodes, replay.Modules);

        var nodes = replay.Nodes;

        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read",
                              nodes.Sum(n => (long)n.Calls),
                              nodes.Length,
                              replay.CallLog.Calls,
                              replay.CallLog.FunctionCount,
                              replay.CallLog.Size / (1024 * 1024),
                              replay.CallLog.LocatorCount);

        var unknownSymbols = await CallstackProcessor.Process(callStack, request.SymbolsPath, messages, cancellationToken);

        LogUnknownSymbols(unknownSymbols);

        var collapsed = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        callStack = callStack.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallStackNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        var planLink = IteratorPlanLinker.Link(replay.CallLog, mapped, events);

        var cropped = false;

        if (request.CropToQuery && QueryExecutionCropper.Crop(callStack) is { } crop)
        {
            callStack = crop.Tree;

            mapped = [.. mapped.Select(node => node is null ? null : crop.Projected.GetValueOrDefault(node))];

            cropped = true;

            Logger.LogInformation("Call tree cropped to the query's execution, {Roots} root(s)", callStack.Root.Children.Count);
        }

        replay.CallLog.MapNodes(mapped);

        replay.CallLog.AnnotateNodes();

        replay.Timeline.MapNodes(mapped);

        callStack.RemoveCallsUnder(IsInstrumentation);

        var timeline = replay.Timeline.WithoutCallsUnder(IsInstrumentation);

        if (cropped)
        {
            timeline = timeline.CroppedTo(callStack) ?? timeline;
        }

        Logger.LogInformation("{Calls} call(s) after removing Extended Events and tracing, {Spans} call span(s) on {Threads} "
                              + "thread(s) in the flame chart",
                              callStack.Nodes().Sum(n => n.Calls),
                              timeline.SpanCount,
                              timeline.Threads.Count);

        timeline.AddCallActivity(QueryRunner.ActivityBuckets);

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

        var memory = MemoryMapper.Map(replay.CallLog, memoryFunctions, timeline, operators, request.MemoryClerks);

        var unidentified = memory.Kinds
                                 .Where(k => k.Name.StartsWith(MemoryMapper.UnknownClerk, StringComparison.Ordinal)
                                             || k.Name == MemoryMapper.Unattributed)
                                 .Aggregate(0ul, (total, k) => total + k.Allocated);

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, operators);

        timeline.SetLifetimes(lifetimes);

        var expanded = events.ExpandOwned();

        var marked = MarkerLinker.Link(replay.Markers, functions.Publishers, request.RawEvents, expanded, timeline);

        timeline.SetEventLinks(marked.Links);

        Logger.LogInformation("{Linked} of {Events} event(s) linked to their trace position: {Written} of {Markers} published "
                              + "event(s) written to the session, {Paired} paired with an event",
                              timeline.EventLinks.Count,
                              request.RawEvents.Count,
                              marked.Written,
                              marked.Markers,
                              marked.Paired);

        Logger.LogInformation("{Matched} of {Operators} operator(s) matched to recorded iterators, {Allocations} allocation(s) of "
                              + "{Bytes}, peak in use {PeakInUse}, {Unidentified} with no memory clerk, {Lifetimes} operator "
                              + "lifetime(s) on {Threads} thread(s)",
                              matched,
                              operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 }),
                              memory.Allocations,
                              Size(memory.Allocated),
                              Size(memory.PeakInUse),
                              Size(unidentified),
                              lifetimes.Count,
                              lifetimes.Select(l => l.Thread).Distinct().Count());

        if (events.Count > 0)
        {
            callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), QueryRunner.ActivityBuckets);
        }

        steps.Complete();

        return new FullTraceResult(callStack, replay.CallLog, timeline) { PlanLink = planLink };
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
