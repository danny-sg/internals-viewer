using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Events;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Debugging.TimeTravel;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Splits;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;
using InternalsViewer.TransactionLog;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Query.Events.Query;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.Parsing.Statements;

namespace InternalsViewer.Query;

public sealed class QueryRunner(ILogger<QueryRunner> logger,
                                EventReader eventReader,
                                LogRecordReader logRecordReader,
                                ColumnstoreService? columnstoreService = null,
                                ColumnstorePageMapper? columnstorePageMapper = null,
                                ITimeTravelRecorder? timeTravelRecorder = null)
{
    private const int ActivityBuckets = 96;

    private const int EventDifferencesReported = 10;

    private const int EventWorkersReported = 4;

    public bool ResolveColumnstorePages { get; set; } = true;

    private ILogger<QueryRunner> Logger { get; } = logger;

    private EventReader EventReader { get; } = eventReader;

    private LogRecordReader LogRecordReader { get; } = logRecordReader;

    private ColumnstorePageMapper? ColumnstorePageMapper { get; } = columnstorePageMapper;

    private ITimeTravelRecorder? TimeTravelRecorder { get; } = timeTravelRecorder;

    public async Task<QueryResult> TraceQuery(ExecuteSqlPayload payload,
                                              DatabaseSource database,
                                              EventOptions eventOptions,
                                              string symbolsPath,
                                              IProgress<ProgressDetail>? progress,
                                              CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(payload.SqlText))
        {
            return new QueryResult
            {
                IsSuccess = false,
                RowCount = 0,
                SessionId = "None",
                Message = "Empty query text"
            };
        }

        var connectionString = database.Connection.GetConnectionString();

        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        long rowCount;

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;
        CallStackTree callStack;
        List<RawEvent> rawEvents;
        List<QueryResultSet> resultSets;
        List<LogRecord> logRecords;

        long? cropStart = null;
        long? cropEnd = null;

        Func<EngineEvent, bool>? endMarker = null;

        var isReplayMode = false;

        var (preCommands, commands, postCommands) = QueryParser.Parse(payload);

        if (!payload.QueryOptions.Trace)
        {
            try
            {
                (rowCount, resultSets) = await RunQueryDirect(payload.SqlText,
                                                              connectionString,
                                                              payload.QueryOptions,
                                                              progress,
                                                              cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new QueryResult { IsSuccess = false, Message = "Query cancelled", SessionId = sessionId };
            }
            catch (SqlException ex)
            {
                var message = $"Msg: {ex.Number}, Level: {ex.Class}, State: {ex.State}, Line: {ex.LineNumber}"
                              + $"{Environment.NewLine}{ex.Message}";

                return new QueryResult { IsSuccess = false, Message = message, SessionId = sessionId };
            }
            catch (Exception ex)
            {
                var message = "Non-Database Error:"
                              + $"{Environment.NewLine}{ex.InnerException?.Message ?? ex.Message}"
                              + $"{Environment.NewLine}{ex.StackTrace}";

                return new QueryResult { IsSuccess = false, Message = message, SessionId = sessionId };
            }

            return new QueryResult
            {
                IsSuccess = true,
                EngineEvents = [],
                ExecutionPlans = [],
                ResultSets = resultSets,
                SessionId = sessionId,
                RowCount = rowCount
            };
        }

        if (commands.Length != 1
            || (payload.TrackedSelection == null
                && payload.StatementType is StatementType.MultiStatementSelect
                                            or StatementType.MultiStatementModification))
        {
            return new QueryResult
            {
                IsSuccess = false,
                RowCount = 0,
                SessionId = "None",
                Message = "Multi-statement queries cannot be traced. Select a single statement then right click and " +
                          "choose 'Trace query selection'."
            };
        }

        if (payload.StatementType == StatementType.Modification)
        {
            endMarker = e =>
                e is BatchStartEvent batchStart &&
                batchStart.SqlText.Contains($"ROLLBACK TRANSACTION iv_{sessionId[..28]}");

            isReplayMode = true;
        }

        ITimeTravelRecording? recording = null;

        TimeTravelTrace? fullTrace = null;

        var memoryClerks = MemoryClerkSnapshot.Empty;

        try
        {
            if (eventOptions.RecordTimeTravel)
            {
                recording = await PrepareTimeTravelRecording(connectionString, progress, cancellationToken);
            }

            if (!payload.QueryOptions.ClearBufferPool)
            {
                await WarmUp(connectionString, preCommands, commands[0], progress, cancellationToken);
            }

            var clerksBefore = recording is null
                ? MemoryClerkSnapshot.Empty
                : await ReadMemoryClerks(connectionString, progress, cancellationToken);

            (var filePath, rowCount, logRecords, resultSets, var timeTravelTrace)
                = await RunQueryWithEventSession(sessionId,
                                                 preCommands,
                                                 commands[0],
                                                 postCommands,
                                                 connectionString,
                                                 isReplayMode,
                                                 payload.QueryOptions,
                                                 eventOptions,
                                                 recording,
                                                 progress,
                                                 cancellationToken);

            fullTrace = timeTravelTrace;

            if (fullTrace is not null)
            {
                memoryClerks = clerksBefore.Merge(await ReadMemoryClerks(connectionString, progress, cancellationToken));
            }

            var eventsStart = Stopwatch.GetTimestamp();

            (events, executionPlans, callStack, rawEvents) = await EventReader.GetEvents(filePath,
                                                                                          connectionString,
                                                                                          database,
                                                                                          eventOptions.IncludeSystemObjects,
                                                                                          progress,
                                                                                          cancellationToken,
                                                                                          endMarker);

            progress?.Report($"{events.Count} event(s) retrieved in {Stopwatch.GetElapsedTime(eventsStart)}");

            await MapColumnstorePages(database, executionPlans, events, progress, cancellationToken);

            events = RowGroupScanGrouper.Group(events);

            if (eventOptions.AutoDeleteTrace && !string.IsNullOrWhiteSpace(eventOptions.TraceDirectory))
            {
                DeleteTraceFiles(filePath, progress);
            }

            if (eventOptions.CropToQuery)
            {
                var (start, end) = QueryCropper.GetCropTiming(events);

                if (events.FirstOrDefault(e => e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 }) is { } query
                    && start.HasValue 
                    && end.HasValue)
                {
                    query.TimeUs = start.Value;
                    query.DurationUs = end.Value - start.Value;

                    cropStart = start;
                    cropEnd = end;
                }
            }

            var includeCallStack = eventOptions.IncludeCallStack;

            if (includeCallStack)
            {
                progress?.Report($"Processing callstack frames");

                var unknownSymbols = await CallstackProcessor.Process(callStack, symbolsPath, progress, cancellationToken);

                var keep = cropStart is null ? null : KeepSet(events);

                callStack = callStack.CollapseToFunctions(keep is null ? null : keep.Contains);

                OperatorCallStackMatcher.Match(events);

                CallStackPlanNodeMatcher.Match(events);

                ReadAheadClassifier.Classify(events);

                LogUnknownSymbols(unknownSymbols);
            }

            AllocationPageClassifier.Classify(events);

            var flattened = RowGroupScanGrouper.Flatten(events);

            ObjectPoolReadLinker.Link(flattened);

            ObjectPoolDurationStamper.Stamp(flattened);

            RowGroupScanGrouper.Fit(events);

            OperatorBoundsExtender.ExtendStarts(events);

            if (cropStart is { } trimStart && cropEnd is { } trimEnd)
            {
                events = [.. events.Where(e => e.TimeUs <= trimEnd && e.TimeUs + e.DurationUs >= trimStart)];
            }

            if (includeCallStack && events.Count > 0)
            {
                // Per-node activity histogram across the query window
                callStack.ComputeActivity(cropStart ?? events.Min(e => e.TimeUs),
                                          cropEnd ?? events.Max(e => e.TimeUs),
                                          buckets: ActivityBuckets);
            }
        }
        catch (OperationCanceledException)
        {
            return new QueryResult
            {
                IsSuccess = false,
                Message = "Query cancelled",
                SessionId = sessionId
            };
        }
        catch (SqlException ex)
        {
            var message = $"Msg: {ex.Number}, Level: {ex.Class}, State: {ex.State}, Line: {ex.LineNumber}"
                          + $"{Environment.NewLine}{ex.Message}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }
        catch (Exception ex)
        {
            var message = "Non-Database Error:"
                          + $"{Environment.NewLine}{ex.InnerException?.Message ?? ex.Message}"
                          + $"{Environment.NewLine}{ex.StackTrace}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }
        finally
        {
            if (recording is not null)
            {
                await recording.DisposeAsync();
            }
        }

        if (logRecords.Count > 0)
        {
            TransactionLogEventMatcher.Match(events, logRecords);

            PageSplitEventMatcher.Match(events, logRecords);
        }

        ExpressionCatalog.Populate(executionPlans, resultSets);

        return new QueryResult
        {
            IsSuccess = true,
            EngineEvents = events,
            ExecutionPlans = executionPlans,
            CallStackTree = fullTrace is null ? callStack : null,
            ResultSets = resultSets,
            LogRecords = logRecords,
            SessionId = sessionId,
            RowCount = rowCount,
            CropStartUs = cropStart,
            CropEndUs = cropEnd,
            FullTrace = fullTrace is null ? null : new PendingFullTrace(fullTrace, events, symbolsPath, memoryClerks, rawEvents)
        };
    }

    public async Task<FullTraceResult?> LoadFullTraceAsync(PendingFullTrace pending,
                                                          IProgress<ProgressDetail>? progress,
                                                          CancellationToken cancellationToken)
    {
        var events = pending.Events;

        var operators = events.OfType<ExecutionOperatorEvent>().ToList();

        progress?.Report("Opening Full Trace");

        var start = Stopwatch.GetTimestamp();

        TimeTravelReplay replay;

        ReplayFunctionSet functions;

        try
        {
            using var session = await TimeTravelSession.OpenAsync(pending.Trace, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report($"Full Trace opened in {Stopwatch.GetElapsedTime(start)}");

            progress?.Report($"Replaying Full Trace for {pending.ThreadIds.Count} thread(s)");

            start = Stopwatch.GetTimestamp();

            functions = await ReplayFunctions.ResolveAsync(session.Modules, pending.SymbolsPath, progress, cancellationToken);

            progress?.Report($"{functions.Excluded.Length:N0} Extended Events and tracing function(s) excluded from the replay, "
                             + $"{functions.Memory.Length:N0} memory function(s) tracked");

            replay = await session.ReplayAsync(pending.ThreadIds, functions, progress, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be replayed", pending.Trace.TracePath);

            progress?.Report($"Full Trace could not be replayed: {exception.Message}");

            return null;
        }

        DeleteTimeTravelTrace(pending.Trace, progress);

        var callStack = new CallStackTree();

        var replayed = TimeTravelCallMerger.Merge(callStack, replay.Calls, ActivityBuckets);

        var nodes = replay.Calls.Nodes;

        progress?.Report($"{nodes.Sum(n => (long)n.Calls):N0} call(s) on {nodes.Length:N0} call path(s) replayed in "
                         + $"{Stopwatch.GetElapsedTime(start)}");

        progress?.Report($"{replay.CallLog.Calls:N0} call(s) of {replay.CallLog.FunctionCount:N0} function(s) logged in "
                         + $"{replay.CallLog.Size / (1024.0 * 1024.0):N1} MB");

        progress?.Report("Processing callstack frames");

        var unknownSymbols = await CallstackProcessor.Process(callStack, pending.SymbolsPath, progress, cancellationToken);

        LogUnknownSymbols(unknownSymbols);

        var collapsed = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        callStack = callStack.CollapseToFunctions(collapsed: (original, node) => collapsed[original] = node);

        CallStackNode?[] mapped = [.. replayed.Select(node => collapsed.GetValueOrDefault(node))];

        replay.CallLog.MapNodes(mapped);

        replay.Timeline.MapNodes(mapped);

        if (pending.RawEvents.Count > 0)
        {
            await ReportExtendedEvents(replay.Timeline, replay.CallLog, functions, pending.RawEvents, progress, cancellationToken);
        }

        callStack.RemoveCallsUnder(node => node.IsExtendedEvents || node.IsTracing);

        progress?.Report($"{callStack.Nodes().Sum(n => n.Calls):N0} call(s) after removing Extended Events and tracing");

        var timeline = replay.Timeline.WithoutCallsUnder(node => node.IsExtendedEvents || node.IsTracing);

        progress?.Report($"{timeline.SpanCount:N0} call span(s) on {timeline.Threads.Count:N0} thread(s) in the flame chart");

        var matched = IteratorInstanceMatcher.Match(callStack, events);

        var planOperators = operators.Count(o => o.PlanNodeIdentifier is { NodeId: >= 0 });

        progress?.Report($"{matched:N0} of {planOperators:N0} operator(s) matched to recorded iterators");

        var memory = TimeTravelMemory.Apply(replay.CallLog, functions.Memory, timeline, operators, pending.MemoryClerks);

        progress?.Report($"{memory.Allocations:N0} allocation(s) of {Size(memory.Bytes)}, peak in use {Size(memory.PeakInUse)}");

        var unidentified = memory.Kinds
                                 .Where(k => k.Name.StartsWith(TimeTravelMemory.UnknownClerk, StringComparison.Ordinal)
                                             || k.Name == TimeTravelMemory.Unattributed)
                                 .Aggregate(0ul, (total, k) => total + k.Allocated);

        if (unidentified > 0)
        {
            progress?.Report($"{Size(unidentified)} allocated with no memory clerk identified");
        }

        var lifetimes = IteratorLifetimes.Build(timeline, operators);

        timeline.SetLifetimes(lifetimes);

        var threads = lifetimes.Select(l => l.Thread).Distinct().Count();

        progress?.Report($"{lifetimes.Count:N0} operator lifetime(s) on {threads:N0} thread(s)");

        if (events.Count > 0)
        {
            callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), ActivityBuckets);
        }

        return new FullTraceResult(callStack, replay.CallLog, timeline);
    }

    private async Task MapColumnstorePages(DatabaseSource database,
                                           List<ExecutionPlan>? executionPlans,
                                           List<EngineEvent> events,
                                           IProgress<ProgressDetail>? progress,
                                           CancellationToken cancellationToken)
    {
        if (!ResolveColumnstorePages
            || executionPlans is null
            || columnstoreService is null
            || ColumnstorePageMapper is null)
        {
            return;
        }

        var units = executionPlans.SelectMany(p => p.NodesById.Values)
                                  .Where(OperatorClassifier.IsColumnstoreScan)
                                  .Select(n => FindAllocationUnit(database, n))
                                  .OfType<AllocationUnit>()
                                  .DistinctBy(u => u.AllocationUnitId)
                                  .ToList();

        if (units.Count == 0)
        {
            return;
        }

        progress?.Report("Resolving columnstore pages");

        foreach (var unit in units)
        {
            var index = await columnstoreService.GetIndex(unit, database, cancellationToken);

            var reads = await ColumnstorePageMapper.MapAsync(database, index, cancellationToken);

            var secondaryDictionaries = index.CompressedRowGroups
                                             .SelectMany(r => r.Segments)
                                             .Where(s => s.SecondaryDictionaryId >= 0)
                                             .Select(s => (s.Key.ColumnId, s.SecondaryDictionaryId, s.Key.RowGroupId));

            ObjectPoolRowGroupResolver.Resolve(events, index.HobtId, secondaryDictionaries);

            ObjectPoolPageLinker.Link(events, index.HobtId, reads, index.DeleteBitmapAllocationUnit);
        }
    }

    private static AllocationUnit? FindAllocationUnit(DatabaseSource database, PlanNode node)
    {
        if (string.IsNullOrEmpty(node.Table))
        {
            return null;
        }

        return database.AllocationUnits
                       .Values
                       .FirstOrDefault(a => NameMatches(a.IndexName, node.Index ?? string.Empty)
                                            && NameMatches(a.TableName, node.Table)
                                            && (string.IsNullOrEmpty(node.Schema) || NameMatches(a.SchemaName, node.Schema))
                                            && a.AllocationUnitType == AllocationUnitType.InRowData);
    }

    private static bool NameMatches(string? left, string right)
        => string.Equals(left?.Trim('[', ']'), right.Trim('[', ']'), StringComparison.OrdinalIgnoreCase);

    private void DeleteTraceFiles(string filePath, IProgress<ProgressDetail>? progress)
    {
        long size = 0;

        try
        {
            var directory = Path.GetDirectoryName(filePath);

            var sessionName = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(sessionName))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, $"{sessionName}*.xel"))
            {
                size += new FileInfo(file).Length;

                File.Delete(file);
            }

            progress?.Report($"Trace file deleted ({size / (1024.0 * 1024.0):N2} MB)");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to delete trace file(s) for {FilePath}", filePath);
        }
    }

    /// <summary>
    /// The events whose call-stack frames survive the crop: if an event survives, so must its call stack
    /// </summary>
    /// <remarks>
    /// A consolidated group is what reaches the top-level list; the raw events it owns do not, yet THEY are the ones
    /// carrying the call-stack frames (a group has none of its own). So every <see cref="IEventGroup"/> is expanded —
    /// any group, not just reads: locks are grouped too, and keying this on one group type silently drops every frame
    /// belonging to a grouped event from the tree, but only when cropping is on (uncropped there is no keep set).
    /// </remarks>
    private static HashSet<EngineEvent> KeepSet(List<EngineEvent> events) => events.ExpandOwned();

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

    private async Task<MemoryClerkSnapshot> ReadMemoryClerks(string connectionString,
                                                           IProgress<ProgressDetail>? progress,
                                                           CancellationToken cancellationToken)
    {
        try
        {
            return await MemoryClerks.ReadAsync(connectionString, cancellationToken);
        }
        catch (SqlException exception)
        {
            Logger.LogWarning(exception, "Memory clerks could not be read");

            progress?.Report($"Memory clerks could not be read: {exception.Message}");

            return MemoryClerkSnapshot.Empty;
        }
    }

    private static async Task ReportExtendedEvents(TimeTravelTimeline timeline,
                                                   TimeTravelCallLog log,
                                                   ReplayFunctionSet functions,
                                                   IReadOnlyList<RawEvent> rawEvents,
                                                   IProgress<ProgressDetail>? progress,
                                                   CancellationToken cancellationToken)
    {
        var summary = ExtendedEventTrace.Summarise(timeline, log, functions.Publishers, functions.BufferReserves, rawEvents);

        progress?.Report($"Extended Events: {summary.Published:N0} published on the recorded thread(s), "
                         + $"{summary.Written:N0} written to this session's buffer 0x{summary.SessionBuffer:X}, "
                         + $"{summary.InFile:N0} in the session file for those thread(s)");

        foreach (var (name, written, inFile) in summary.Differences.Take(EventDifferencesReported))
        {
            progress?.Report($"    {name}: {written:N0} written, {inFile:N0} in the file");
        }

        progress?.Report($"{summary.Workers.Select(w => w.Worker).Distinct().Count():N0} worker(s) and {summary.Tasks:N0} task(s) "
                         + $"on {summary.Workers.Select(w => w.Thread).Distinct().Count():N0} thread(s)");

        foreach (var (worker, thread) in summary.Workers.Take(EventWorkersReported))
        {
            var uses = await log.FindAsync(worker, cancellationToken);

            progress?.Report($"    Worker 0x{worker:X} on thread {thread}: in {uses.Count:N0} logged call value(s)");
        }
    }

    private static string Size(ulong bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):N1} MB" : $"{bytes / 1024.0:N1} KB";

    private void DeleteTimeTravelTrace(TimeTravelTrace trace, IProgress<ProgressDetail>? progress)
    {
        try
        {
            var size = new FileInfo(trace.TracePath).Length;

            File.Delete(trace.TracePath);

            progress?.Report($"Time travel trace deleted ({size / (1024.0 * 1024.0):N0} MB)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(exception, "Time travel trace {TracePath} could not be deleted", trace.TracePath);
        }
    }

    private async Task<(string, long, List<LogRecord> logRecords, List<QueryResultSet> resultSets, TimeTravelTrace? timeTravelTrace)>
        RunQueryWithEventSession(string sessionName,
                                 string[] preCommandSql,
                                 string commandSql,
                                 string[] postCommandSql,
                                 string connectionString,
                                 bool isReplayMode,
                                 QueryOptions queryOptions,
                                 EventOptions eventOptions,
                                 ITimeTravelRecording? recording,
                                 IProgress<ProgressDetail>? progress,
                                 CancellationToken cancellationToken)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        SqlInfoMessageEventHandler onInfoMessage = (_, e) => progress?.Report(e.Message);

        await connection.OpenAsync(cancellationToken);

        var directory = string.IsNullOrWhiteSpace(eventOptions.TraceDirectory)
                        ? await connection.ExecuteScalar<string>(EventSql.GetFileLocationSql(), cancellationToken)
                        : eventOptions.TraceDirectory.TrimEnd('\\', '/');

        var separator = directory?.StartsWith('/') == true ? '/' : '\\';

        var filePath = $"{directory}{separator}{sessionName}.xel";

        List<LogRecord> logRecords = [];

        string? startLsn = null;

        List<QueryResultSet> resultSets;

        TimeTravelTrace? timeTravelTrace = null;

        if (preCommandSql.Length > 0)
        {
            connection.InfoMessage += onInfoMessage;

            progress?.Report("Pre-Trace: ");

            var preStart = Stopwatch.GetTimestamp();

            foreach (var preCommand in preCommandSql)
            {
                var itemRowCount = await connection.ExecuteSql(preCommand, cancellationToken, Logger);

                if (itemRowCount > -1)
                {
                    progress?.Report($"  {itemRowCount} row(s) affected");
                }
            }

            connection.InfoMessage -= onInfoMessage;

            Logger.LogDebug("Pre-Commands executed in {Duration}", Stopwatch.GetElapsedTime(preStart));
        }

        var spid = await connection.ExecuteScalar<short>("SELECT @@SPID", cancellationToken, Logger);

        var createSessionSql = EventSql.GetCreateSessionSql(sessionName, filePath, spid, isReplayMode, eventOptions);

        await connection.ExecuteSql(createSessionSql, cancellationToken, Logger);

        if (queryOptions.ClearBufferPool | isReplayMode)
        {
            // Flush dirty pages either for DROPCLEANBUFFERS or to write the transaction log to disk 
            await connection.ExecuteSql("CHECKPOINT", cancellationToken, Logger);
        }

        if (queryOptions.ClearBufferPool)
        {
            // Removes all pages from the buffer pool so pages will come from I/O rather than the cache
            await connection.ExecuteSql("DBCC DROPCLEANBUFFERS", cancellationToken, Logger);
        }

        if (queryOptions.DisableReadAhead)
        {
            // Disable pre-fetching page scans for the session
            await connection.ExecuteSql("DBCC TRACEON(652)", cancellationToken, Logger);
        }

        if (isReplayMode)
        {
            startLsn = await connection.ExecuteScalar<string?>(
                "SELECT MAX([Current LSN]) FROM fn_dblog(NULL, NULL);", cancellationToken, Logger);

            progress?.Report($"Start LSN: {startLsn}");
        }

        // Session try/catch block that should stop the session if there is any failure
        try
        {
            if (recording is not null)
            {
                progress?.Report("Starting time travel recording");

                await recording.StartAsync(cancellationToken);

                progress?.Report("Time travel recording started");
            }

            await connection.ExecuteSql(EventSql.GetStartSessionSql(sessionName), cancellationToken, Logger);

            if (isReplayMode)
            {
                progress?.Report($"Transaction started");

                await connection.ExecuteSql($"BEGIN TRANSACTION iv_{sessionName[..28]};", cancellationToken, Logger);
            }

            await Task.Delay(250, cancellationToken);

            Logger.LogDebug("SQL: {Sql}", commandSql);

            var queryStart = Stopwatch.GetTimestamp();

            var command = new SqlCommand(commandSql, connection);

            command.CommandTimeout = 0;

            connection.InfoMessage += onInfoMessage;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            resultSets = [];

            do
            {
                if (queryOptions.IncludeResults)
                {
                    var columns = reader.GetResultColumns();

                    var stringPools = BuildStringPools(columns);

                    var rows = new List<ResultRow<long>>();

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        rowCount++;

                        var values = new object?[columns.Count];

                        for (var i = 0; i < columns.Count; i++)
                        {
                            var rawValue = reader.IsDBNull(i) ? null : reader.GetValue(i);

                            if (rawValue is string s && stringPools.TryGetValue(i, out var pool))
                            {
                                rawValue = InternString(pool, s);
                            }

                            values[i] = rawValue;
                        }

                        rows.Add(new ResultRow<long>(values));
                    }

                    resultSets.Add(new QueryResultSet { Columns = columns, Rows = rows});
                }
                else
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        rowCount++;
                    }
                }
            } while (await reader.NextResultAsync(cancellationToken));

            await reader.CloseAsync();

            connection.InfoMessage -= onInfoMessage;

            progress?.Report($"Query executed in: {Stopwatch.GetElapsedTime(queryStart)}");

            if (recording is not null)
            {
                timeTravelTrace = await recording.StopAsync(cancellationToken);

                var traceSize = new FileInfo(timeTravelTrace.TracePath).Length / 1024d / 1024d;

                progress?.Report($"Time travel trace: {timeTravelTrace.TracePath} ({traceSize:N0} MB)");
            }

            if (isReplayMode)
            {
                logRecords = await LogRecordReader.GetLogRecords(connection, startLsn, sessionName);

                progress?.Report($"{logRecords.Count} log record(s) retrieved");

                await connection.ExecuteSql($"ROLLBACK TRANSACTION iv_{sessionName[..28]};",
                                            cancellationToken,
                                            Logger);

                progress?.Report($"Transaction rolled back");
            }
        }
        finally
        {
            // Cleanup must run even when the query was cancelled, so it must not observe the (now cancelled) token - otherwise the
            // Extended Events session is left running on the server.
            try
            {
                await connection.ExecuteSql(EventSql.GetStopSessionSql(sessionName), CancellationToken.None, Logger);
            }
            catch
            {
                // No-op
            }

            try
            {
                await connection.ExecuteSql(EventSql.GetDropSessionSql(sessionName), CancellationToken.None, Logger);
            }
            catch
            {
                // No-op
            }
        }

        if (postCommandSql.Length > 0)
        {
            progress?.Report("Post-Trace: ");

            foreach (var postCommand in postCommandSql)
            {
                var itemRowCount = await connection.ExecuteSql(postCommand, cancellationToken, Logger);

                if (itemRowCount > -1)
                {
                    progress?.Report($"  {itemRowCount} row(s) affected");
                }
            }
        }

        return (filePath, rowCount, logRecords, resultSets, timeTravelTrace);
    }

    private async Task WarmUp(string connectionString,
                              string[] preCommandSql,
                              string commandSql,
                              IProgress<ProgressDetail>? progress,
                              CancellationToken cancellationToken)
    {
        progress?.Report("Warming up: running the query once, untraced and rolled back");

        var start = Stopwatch.GetTimestamp();

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        foreach (var preCommand in preCommandSql)
        {
            await connection.ExecuteSql(preCommand, cancellationToken, Logger);
        }

        await connection.ExecuteSql("BEGIN TRANSACTION;", cancellationToken, Logger);

        try
        {
            await using var command = new SqlCommand(commandSql, connection);

            command.CommandTimeout = 0;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            do
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                }
            }
            while (await reader.NextResultAsync(cancellationToken));
        }
        finally
        {
            await connection.ExecuteSql("IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;", CancellationToken.None, Logger);
        }

        progress?.Report($"Warm-up finished in {Stopwatch.GetElapsedTime(start)}");
    }

    private async Task<ITimeTravelRecording?> PrepareTimeTravelRecording(string connectionString,
                                                                        IProgress<ProgressDetail>? progress,
                                                                        CancellationToken cancellationToken)
    {
        if (TimeTravelRecorder is null)
        {
            progress?.Report("Time travel recording is not available");

            return null;
        }

        progress?.Report("Preparing time travel recording");

        return await TimeTravelRecorder.PrepareAsync(connectionString, cancellationToken);
    }

    private static async Task<(long RowCount, List<QueryResultSet> ResultSets)> 
        RunQueryDirect(string commandSql,
                       string connectionString,
                       QueryOptions queryOptions,
                       IProgress<ProgressDetail>? progress,
                       CancellationToken cancellationToken)
    {
        long rowCount = 0;

        List<QueryResultSet> resultSets = [];

        await using var connection = new SqlConnection(connectionString);

        connection.InfoMessage += (_, e) => progress?.Report(e.Message);

        await connection.OpenAsync(cancellationToken);

        var commands = QueryParser.SplitCommands(commandSql);

        foreach (var sql in commands)
        {
            var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            do
            {
                if (queryOptions.IncludeResults)
                {
                    var columns = reader.GetResultColumns();

                    var stringPools = BuildStringPools(columns);
                    
                    var rows = new List<ResultRow<long>>();

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        rowCount++;

                        var values = new object?[columns.Count];

                        for (var i = 0; i < columns.Count; i++)
                        {
                            var rawValue = reader.IsDBNull(i) ? null : reader.GetValue(i);

                            if (rawValue is string s && stringPools.TryGetValue(i, out var pool))
                            {
                                rawValue = InternString(pool, s);
                            }

                            values[i] = rawValue;
                        }

                        rows.Add(new ResultRow<long>(values));
                    }

                    resultSets.Add(new QueryResultSet { Columns = columns, Rows = rows });
                }
                else
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        rowCount++;
                    }
                }
            }
            while (await reader.NextResultAsync(cancellationToken));

            await reader.CloseAsync();
        }

        return (rowCount, resultSets);
    }

    private static Dictionary<int, Dictionary<string, string>> BuildStringPools(List<ResultColumn> columns)
    {
        var pools = new Dictionary<int, Dictionary<string, string>>();

        foreach (var col in columns)
        {
            if (col.ClrType == typeof(string))
            {
                pools[col.Ordinal] = new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        return pools;
    }

    private static string InternString(Dictionary<string, string> pool, string value)
    {
        if (!pool.TryGetValue(value, out var interned))
        {
            pool[value] = value;

            return value;
        }

        return interned;
    }
}