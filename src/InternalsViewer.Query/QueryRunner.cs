using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Batches;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Parsing.Plans;
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

        try
        {
            (var filePath, rowCount, logRecords, resultSets)
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
                                                                              progress,
                                                                              cancellationToken,
                                                                              endMarker);

            progress?.Report($"{events.Count} event(s) retrieved in {Stopwatch.GetElapsedTime(eventsStart)}");

            if (eventOptions.AutoDeleteTrace && !string.IsNullOrWhiteSpace(eventOptions.TraceDirectory))
            {
                DeleteTraceFiles(filePath, progress);
            }

            events = EventFilter.Filter(events, eventOptions);

            if (eventOptions.CropToQuery)
            {
                var (start, end) = QueryCropper.GetCropTiming(events);

                if (events.FirstOrDefault(e => e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 }) is { } query
                    && start.HasValue 
                    && end.HasValue)
                {
                    query.TimeUs = start.Value;
                    query.DurationUs = end.Value - start.Value;
                }
            }

            if (eventOptions.IncludeCallStack)
            {
                progress?.Report($"Processing callstack frames");

                var unknownSymbols = await CallstackProcessor.Process(callStack, symbolsPath, progress, cancellationToken);

                var keep = cropStart is null ? null : KeepSet(events);

                callStack = callStack.CollapseToFunctions(keep is null ? null : keep.Contains);

                OperatorCallStackMatcher.Match(events);

                if (events.Count > 0)
                {
                    // Per-node activity histogram across the query window
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

            if (cropStart is { } trimStart && cropEnd is { } trimEnd)
            {
                events = events.Where(e => e.TimeUs <= trimEnd && e.TimeUs + e.DurationUs >= trimStart).ToList();
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
            CallStackTree = callStack,
            ResultSets = resultSets,
            LogRecords = logRecords,
            SessionId = sessionId,
            RowCount = rowCount,
            CropStartUs = cropStart,
            CropEndUs = cropEnd
        };
    }

    private void DeleteTraceFiles(string filePath, IProgress<string>? progress)
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

        var directory = string.IsNullOrWhiteSpace(eventOptions.TraceDirectory)
                        ? await connection.ExecuteScalar<string>(EventSql.GetFileLocationSql(), cancellationToken)
                        : eventOptions.TraceDirectory.TrimEnd('\\');

        var filePath = $"{directory}\\{sessionName}.xel";

        List<LogRecord> logRecords = [];

        string? startLsn = null;

        List<QueryResultSet> resultSets;

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

        return (filePath, rowCount, logRecords, resultSets);
    }

    private async Task<(long RowCount, List<QueryResultSet> ResultSets)> RunQueryDirect(string commandSql,
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