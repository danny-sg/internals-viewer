using System.Text;

namespace InternalsViewer.Query;

internal static class EventSql
{
    internal static string GetFileLocationSql()
    {
        return @"
                SELECT LEFT(
                    CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)),
                    LEN(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)))
                    - PATINDEX('%[\/]%', REVERSE(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000))))
            );";
    }

    internal static string GetDropSessionSql(string sessionName)
    {
        return $"DROP EVENT SESSION [{sessionName}] ON SERVER;";
    }

    internal static string GetStartSessionSql(string sessionName)
    {
        return $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = START;";
    }

    internal static string GetStopSessionSql(string sessionName)
    {
        return $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP;";
    }

    internal static string GetCreateSessionSql(string sessionName,
                                               string filePath,
                                               short spid,
                                               bool isReplayMode,
                                               EventOptions eventOptions)
    {

        var sessionEvents = new List<string>(EventConstants.Events);

        var sessionActions = new List<string>(EventConstants.Actions);

        if (isReplayMode)
        {
            sessionEvents.AddRange(EventConstants.LogEvents);
        }

        // Always include lock, wait, and latch events for event grouping
        sessionEvents.AddRange(EventConstants.LockEvents);

        sessionEvents.AddRange(EventConstants.WaitEvents);

        sessionEvents.AddRange(EventConstants.LatchEvents);

        if (eventOptions.IncludeMemory)
        {
            sessionEvents.AddRange(EventConstants.MemoryEvents);
        }

        if (eventOptions.IncludeBatchMode)
        {
            sessionEvents.AddRange(EventConstants.BatchModeEvents);
        }

        if (eventOptions.IncludeCallStack)
        {
            sessionActions.AddRange(EventConstants.CallstackActions);
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"CREATE EVENT SESSION [{sessionName}] ON SERVER");

        for (var i = 0; i < sessionEvents.Count; i++)
        {
            var eventName = sessionEvents[i];

            stringBuilder.Append($"ADD EVENT {eventName}");

            if (sessionActions.Count > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("(\n");

                stringBuilder.Append("\n    ACTION (");

                if (EventConstants.CallstackExcludeEvents.Contains(eventName))
                {
                    stringBuilder.Append(string.Join(", ",
                                                     sessionActions.Except(EventConstants.CallstackActions)));
                }
                else
                {
                    stringBuilder.Append(string.Join(", ", sessionActions));
                }

                stringBuilder.Append(")\n");
                stringBuilder.Append("    WHERE (");

                stringBuilder.Append($"sqlserver.session_id = {spid}");
                stringBuilder.Append($" AND sqlserver.sql_text NOT LIKE '%{sessionName}%'");

                stringBuilder.Append(')');

                stringBuilder.Append("\n)");
            }

            if (i < sessionEvents.Count - 1)
            {
                stringBuilder.AppendLine(",");
            }
            else
            {
                stringBuilder.AppendLine();
            }
        }

        // MAX_FILE_SIZE is in MB; when unset (0) SQL Server's default (1 GB) applies.
        var maxFileSize = eventOptions.MaxTraceSizeMb > 0
            ? $"\n       ,MAX_FILE_SIZE      = ({eventOptions.MaxTraceSizeMb})"
            : string.Empty;

        stringBuilder.AppendLine($@"
ADD TARGET package0.event_file
(
    SET FILENAME           = '{filePath}'{maxFileSize}
       ,MAX_ROLLOVER_FILES = (1)
);");

        return stringBuilder.ToString();
    }
}