using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
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
        var messages = new StepLoggerProgress(progress, Logger);

        var steps = new StepProgress(messages, Steps);

        var replayed = await ReplayAsync(request, customHooks, steps, messages, progress, cancellationToken);

        if (replayed is null)
        {
            return null;
        }

        var (replay, functions) = replayed.Value;

        steps.Begin(CallTreeStep);

        var (callStack, mapped) = await BuildCallStackAsync(replay, request.SymbolsPath, messages, cancellationToken);

        var timeline = BuildTimeline(replay, callStack, mapped);

        steps.Complete();

        steps.Begin(OperatorsStep);

        var result = BuildResult(replay, functions, request, callStack, timeline);

        steps.Complete();

        Logger.LogInformation("{Objects} HoBt access object(s) linked to their HoBt", result.HoBtAccess.Count);

        Logger.LogInformation("{Entries} memory map entr(ies) from allocations, constructors, member calls and declared types",
                              result.MemoryMap.Count);

        Logger.LogInformation("{Sites} allocation(s) labelled with their source line, {Vtables} named by their vtable",
                              result.MemoryMap.GetEntries().Count(e => e is { Operation: MemoryMapOperation.Allocated, Site: not null }),
                              replay.MemoryMap.Count(e => e is { Operation: MemoryMapOperation.Allocated, ClassName: not null }));

        Logger.LogInformation("{Captures} custom hook capture(s) read from {Hooks} hook(s)", result.CustomHooks.Count, customHooks.Count);

        return result;
    }

    private async Task<(ReplayResult Replay, ReplayFunctions Functions)?> ReplayAsync(TraceRequest request,
                                                                                      IReadOnlyList<CustomHookDefinition> customHooks,
                                                                                      StepProgress steps,
                                                                                      StepLoggerProgress messages,
                                                                                      IProgress<ProgressDetail>? progress,
                                                                                      CancellationToken cancellationToken)
    {
        try
        {
            steps.Begin(OpeningStep);

            using var session = await ReplaySession.OpenAsync(request.TraceFile, cancellationToken);

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
                                  functions.MemoryFunctions.Length);

            steps.Complete();

            steps.Begin(ReplayStep);

            Logger.LogInformation("Replaying Full Trace for {Threads} thread(s)", request.ThreadIds.Count);

            var replay = await session.ReplayAsync(request.ThreadIds, functions, customHooks, steps, cancellationToken);

            steps.Complete();

            return (replay, functions);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be replayed", request.TraceFile.Path);

            steps.Complete();

            progress?.Report($"Full Trace could not be replayed: {exception.Message}");

            return null;
        }
    }

    private async Task<(CallStackTree CallStack, CallStackNode?[] Mapped)> BuildCallStackAsync(ReplayResult replay,
                                                                                               string symbolsPath,
                                                                                               StepLoggerProgress messages,
                                                                                               CancellationToken cancellationToken)
    {
        var (callStack, replayed) = CallTreeBuilder.Build(replay.Nodes, replay.Modules);

        Logger.LogInformation("{Calls} call(s) on {Paths} call path(s) replayed, {Logged} call(s) of {Functions} function(s) logged in "
                              + "{Size} MB, {Locators} page and row id(s) read",
                              replay.Nodes.Sum(n => (long)n.Calls),
                              replay.Nodes.Length,
                              replay.CallStore.Calls,
                              replay.CallStore.FunctionCount,
                              replay.CallStore.Size / (1024 * 1024),
                              replay.CallStore.LocatorCount);

        var unknownSymbols = await CallstackProcessor.Process(callStack, symbolsPath, messages, cancellationToken);

        LogUnknownSymbols(unknownSymbols);

        LogUnknownEnums(callStack, symbolsPath);

        var collapsed = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        callStack = callStack.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallStackNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        return (callStack, mapped);
    }

    private TraceTimeline BuildTimeline(ReplayResult replay, CallStackTree callStack, CallStackNode?[] mapped)
    {
        replay.CallStore.MapNodes(mapped);

        replay.CallStore.AnnotateNodes();

        replay.Timeline.MapNodes(mapped);

        callStack.RemoveCallsUnder(Instrumentation.IsExcluded);

        var timeline = replay.Timeline.WithoutCallsUnder(Instrumentation.IsExcluded);

        Logger.LogInformation("{Calls} call(s) after removing Extended Events and tracing, {Spans} call span(s) on {Threads} "
                              + "thread(s) in the trace timeline",
                              callStack.Nodes().Sum(n => n.Calls),
                              timeline.SpanCount,
                              timeline.Threads.Count);

        return timeline;
    }

    private TraceResult BuildResult(ReplayResult replay,
                                    ReplayFunctions functions,
                                    TraceRequest request,
                                    CallStackTree callStack,
                                    TraceTimeline timeline)
    {
        var events = request.Events;

        var operators = events.OfType<ExecutionOperatorEvent>().ToList();

        var (memory, allocationCount, allocatedBytes) = AllocationMapper.Map(replay.CallStore,
                                                                             functions.MemoryFunctions,
                                                                             timeline,
                                                                             request.MemoryClerks);

        var builder = new TraceResultBuilder(callStack, replay.CallStore, timeline, events, replay.PlanNodes, replay.Teardowns)
        {
            Memory = memory,
            MemoryMap = MemoryMap.Build(replay.MemoryMap, timeline.NodeOf, GetAllocation(replay.CallStore, functions.MemoryFunctions)),
            ObjectPool = request.ObjectPool,
            CustomHooks = replay.CustomHooks
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
                              Size(allocatedBytes),
                              lifetimes.Count,
                              lifetimes.Select(l => l.Thread).Distinct().Count());

        if (events.Count > 0)
        {
            callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), QueryRunner.ActivityBuckets);
        }

        return builder.Build(outputStructures);
    }

    private List<TraceOutputStructure> ResolveOutputStructures(ReplayResult replay, MemoryIndex memory, TraceOperatorLifetimes lifetimes)
    {
        var allocations = IteratorAllocations.Build(memory,
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

    private static Func<AddressEntry, CallStackNode?, (ulong Bytes, string? Site)> GetAllocation(TraceCallStore store,
                                                                                                IReadOnlyList<MemoryFunction> functions)
    {
        var allocators = new Dictionary<ulong, MemoryFunction>();

        foreach (var function in functions)
        {
            allocators.TryAdd(function.Address, function);
        }

        return (entry, node) =>
        {
            if (node?.Frame is not { } frame
                || !allocators.TryGetValue(frame.Address, out var allocator)
                || store.GetCallList(frame.Address, frame.Instance) is not { } calls
                || entry.CallIndex < 0
                || entry.CallIndex >= calls.Count)
            {
                return (0, null);
            }

            var call = calls[entry.CallIndex];

            return (allocator.GetByteCount(call), allocator.GetSite(call));
        };
    }

    private static string Size(ulong bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):N1} MB" : $"{bytes / 1024.0:N1} KB";
}
