using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query;

public sealed record ExecuteSqlPayload(string SqlText,
                                       QueryOptions QueryOptions,
                                       StatementType StatementType,
                                       TrackedSelectionRange? TrackedSelection);

public sealed record TrackedSelectionRange(int Start, int End);

public sealed class QueryRunner(ILogger<QueryRunner> logger,
                                EventReader eventReader,
                                LogRecordReader logRecordReader)
{
    private ILogger<QueryRunner> Logger { get; } = logger;

    private EventReader EventReader { get; } = eventReader;

    private LogRecordReader LogRecordReader { get; } = logRecordReader;

    public async Task<QueryResult> TraceQuery(ExecuteSqlPayload payload,
                                              DatabaseSource database,
                                              EventOptions eventOptions,
                                              string symbolsPath,
                                              IProgress<string>? progress,
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
        List<QueryResultSet> resultSets;

        // The executed query's time window when cropping — trims surrounding-noise events and call-stack frames.
        long? cropStart = null;
        long? cropEnd = null;

        Func<EngineEvent, bool>? endMarker = null;

        var isReplayMode = false;

        var (preCommands, commands, postCommands) = PayloadParser.Parse(payload);

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

        try
        {
            (var filePath, rowCount, var logRecords, resultSets)
                = await RunQueryWithEventSession(sessionId,
                                                 preCommands,
                                                 commands[0],
                                                 postCommands,
                                                 connectionString,
                                                 isReplayMode,
                                                 payload.QueryOptions,
                                                 eventOptions,
                                                 progress,
                                                 cancellationToken);

            var eventsStart = Stopwatch.GetTimestamp();

            (events, executionPlans, callStack) = await EventReader.GetEvents(filePath,
                                                                              connectionString,
                                                                              database,
                                                                              eventOptions.IncludeSystemObjects,
                                                                              cancellationToken,
                                                                              endMarker);

            progress?.Report($"{events.Count} event(s) retrieved in {Stopwatch.GetElapsedTime(eventsStart)}");

            // Trim events (and, below, the call stack) to the executed query's window so surrounding noise —
            // compilation, other statements, background work the trace also captured — is dropped.
            //
            // "The query's window" is a time span, but it is NOT the whole story: an event carrying this query's
            // plan_handle belongs to the query even when its timestamp lands outside the span. That happens because the
            // window is derived from the spread operator layout while the events keep their own times — a read whose
            // spread position drifts out, or a thread sample (query_thread_profile, not laid out by SpreadEvents) taken
            // before the first page read. Dropping those on time alone strips the reads and entry stacks the timeline's
            // Plan Operators view and the call stack need. Key this off the RAW PlanHandleId (set at parse time from the
            // plan_handle action, on every event) — NOT PlanNodeIdentifier, which only exists once EventPlanNodeMatcher
            // resolves an event to an operator, and that resolution leans on flaky page→allocation-unit lookups; when it
            // misses the reads (no AU) they would be cropped and the call stack/icicle would come out empty.
            if (eventOptions.CropToQuery && CropWindow(events) is var (start, end, planHandle))
            {
                // An event belongs to the query's execution when its span OVERLAPS the window — something already in
                // progress when the statement begins (a lock/latch acquired just before the first read, or a read still
                // completing as it ends). Overlap = starts on/before the window end AND ends on/after its start; a
                // zero-duration event reduces to plain containment.
                bool Overlaps(EngineEvent e) => e.TimeUs <= end && e.TimeUs + e.DurationUs >= start;

                // Keep the overlapping events, PLUS non-lock events carrying the plan_handle — that clause rescues a
                // read/call-stack whose position sits outside the operator window. Locks are kept only by overlap (a
                // plan-handle lock can be a compile-phase schema lock taken before the statement).
                events = events
                    .Where(e => Overlaps(e)
                                || (e is not LockEvent
                                    && planHandle != PlanHandleRegistry.None
                                    && e.PlanHandleId == planHandle))
                    .ToList();

                // query_thread_profile / memory-grant events are END-anchored — their TimeUs is the operator close and
                // DurationUs the elapsed leading up to it — so their real end is TimeUs; TimeUs + DurationUs would
                // double-count the elapsed and push the crop a whole statement-length past the true end.
                static long EndUs(EngineEvent e) => e is QueryThreadEvent or MemoryEvent ? e.TimeUs : e.TimeUs + e.DurationUs;

                // The axis brackets the events that OVERLAP the window (the query's own execution, plus a lock that
                // straddles its start) — NOT the plan-handle stragglers kept only for the call stack. A compile-phase
                // read carrying the plan_handle sits back near time 0 and would otherwise drag cropStart there, leaving
                // an empty gap before the query.
                var windowEvents = events.Where(Overlaps).ToList();

                cropStart = windowEvents.Count > 0 ? Math.Min(start, windowEvents.Min(e => e.TimeUs)) : start;
                cropEnd = windowEvents.Count > 0 ? Math.Max(end, windowEvents.Max(EndUs)) : end;
            }

            if (eventOptions.IncludeCallStack)
            {
                progress?.Report($"Processing callstack frames");

                var unknownSymbols = await CallstackProcessor.Process(callStack, symbolsPath, progress, cancellationToken);

                // Now the frames are resolved, merge each function's call sites into one node (and repoint events);
                // when cropped, only the surviving events' frames are carried over, trimming the tree to the query.
                var keep = cropStart is null ? null : KeepSet(events);

                callStack = callStack.CollapseToFunctions(keep is null ? null : keep.Contains);

                if (events.Count > 0)
                {
                    // Per-node activity histogram across the query window (24 buckets, 14px tall).
                    callStack.ComputeActivity(events.Min(e => e.TimeUs), events.Max(e => e.TimeUs), buckets: 24, height: 14);
                }

                if (Logger.IsEnabled(LogLevel.Debug) && unknownSymbols.Length > 0)
                {
                    foreach (var symbol in unknownSymbols)
                    {
                        Logger.LogDebug($"Unknown symbol: {symbol}");
                    }
                }
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

        return new QueryResult
        {
            IsSuccess = true,
            EngineEvents = events,
            ExecutionPlans = executionPlans,
            CallStack = callStack,
            ResultSets = resultSets,
            SessionId = sessionId,
            RowCount = rowCount,
            CropStartUs = cropStart,
            CropEndUs = cropEnd
        };
    }

    // Padding kept either side of the query window so an event landing just on its boundary is not clipped.
    private const long CropPaddingUs = 100;

    // The executed query's time window, taken from the whole-query operator event (plan NodeId -1) and padded, plus the
    // plan it belongs to (so plan-matched events can be kept for the call stack regardless of timestamp), or null.
    private static (long Start, long End, short PlanHandle)? CropWindow(List<EngineEvent> events)
    {
        var queryNode = events.FirstOrDefault(e => e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 });

        return queryNode?.PlanNodeIdentifier is not { } id
            ? null
            : (Math.Max(0, queryNode.TimeUs - CropPaddingUs), queryNode.TimeUs + queryNode.DurationUs + CropPaddingUs,
               id.PlanHandleId);
    }

    // The events whose call-stack frames survive the crop: the kept events, plus the child events inside each read
    // group (they carry the read's frames but are not themselves in the top-level list).
    private static HashSet<EngineEvent> KeepSet(List<EngineEvent> events)
    {
        var keep = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var e in events)
        {
            keep.Add(e);

            if (e is ReadEventGroup group)
            {
                foreach (var child in group.Events)
                {
                    keep.Add(child);
                }
            }
        }

        return keep;
    }

  

    private async Task<(string, long, List<LogRecord> logRecords, List<QueryResultSet> resultSets)>
        RunQueryWithEventSession(string sessionName,
                                 string[] preCommandSql,
                                 string commandSql,
                                 string[] postCommandSql,
                                 string connectionString,
                                 bool isReplayMode,
                                 QueryOptions queryOptions,
                                 EventOptions eventOptions,
                                 IProgress<string>? progress,
                                 CancellationToken cancellationToken)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        connection.FireInfoMessageEventOnUserErrors = true;

        SqlInfoMessageEventHandler onInfoMessage = (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                progress?.Report(error.Message);
            }
        };

        await connection.OpenAsync(cancellationToken);

        var logPath = await connection.ExecuteScalar<string>(EventSql.GetFileLocationSql(), cancellationToken);

        var filePath = $"{logPath}\\{sessionName}.xel";

        List<LogRecord> logRecords = [];

        string? startLsn = null;

        List<QueryResultSet> resultSets;

        if (preCommandSql.Length > 0)
        {
            connection.InfoMessage += onInfoMessage;

            progress?.Report("Pre-Trace: ");

            foreach (var preCommand in preCommandSql)
            {
                var itemRowCount = await connection.ExecuteSql(preCommand, cancellationToken, Logger);

                if (itemRowCount > -1)
                {
                    progress?.Report($"  {itemRowCount} row(s) affected");
                }
            }

            connection.InfoMessage -= onInfoMessage;
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
                    var columns = ReadSchema(reader);

                    var stringPools = BuildStringPools(columns);

                    var rows = new List<ResultRow>();

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

                        rows.Add(new ResultRow(values));
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
            } while (await reader.NextResultAsync(cancellationToken));

            await reader.CloseAsync();

            connection.InfoMessage -= onInfoMessage;

            connection.FireInfoMessageEventOnUserErrors = false;

            progress?.Report($"Query executed in: {Stopwatch.GetElapsedTime(queryStart)}");

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
            // Cleanup must run even when the query was cancelled, so it must not observe the (now cancelled)
            // token - otherwise the Extended Events session is left running on the server.
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

        return (filePath, rowCount, logRecords, resultSets);
    }

    private async Task<(long RowCount, List<QueryResultSet> ResultSets)>
        RunQueryDirect(string commandSql,
                       string connectionString,
                       QueryOptions queryOptions,
                       IProgress<string>? progress,
                       CancellationToken cancellationToken)
    {
        long rowCount = 0;

        List<QueryResultSet> resultSets = [];

        await using var connection = new SqlConnection(connectionString);

        connection.FireInfoMessageEventOnUserErrors = true;

        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                progress?.Report(error.Message);
            }
        };

        await connection.OpenAsync(cancellationToken);

        var commands = PayloadParser.SplitCommands(commandSql);

        foreach (var sql in commands)
        {
            var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            do
            {
                if (queryOptions.IncludeResults)
                {
                    var columns = ReadSchema(reader);
                    var stringPools = BuildStringPools(columns);
                    var rows = new List<ResultRow>();

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

                        rows.Add(new ResultRow(values));
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

    private static List<ResultColumn> ReadSchema(SqlDataReader reader)
    {
        var schemaTable = reader.GetSchemaTable();

        var columns = new List<ResultColumn>(reader.FieldCount);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var typeName = reader.GetDataTypeName(i);
            var clrType = reader.GetFieldType(i) ?? typeof(object);
            var nullable = schemaTable?.Rows[i]["AllowDBNull"] is true;

            columns.Add(new ResultColumn(i, name, typeName, clrType, nullable));
        }

        return columns;
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