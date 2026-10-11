using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class EventSqlTests
{
    [Fact]
    public void GetStartSessionSql_References_Session_By_Name()
    {
        var sql = EventSql.GetStartSessionSql("MySession");

        Assert.Equal("ALTER EVENT SESSION [MySession] ON SERVER STATE = START;", sql);
    }

    [Fact]
    public void GetStopSessionSql_References_Session_By_Name()
    {
        var sql = EventSql.GetStopSessionSql("MySession");

        Assert.Equal("ALTER EVENT SESSION [MySession] ON SERVER STATE = STOP;", sql);
    }

    [Fact]
    public void GetDropSessionSql_References_Session_By_Name()
    {
        var sql = EventSql.GetDropSessionSql("MySession");

        Assert.Equal("DROP EVENT SESSION [MySession] ON SERVER;", sql);
    }

    [Fact]
    public void GetFileLocationSql_Queries_ErrorLogFileName_ServerProperty()
    {
        var sql = EventSql.GetFileLocationSql();

        Assert.Contains("SERVERPROPERTY('ErrorLogFileName')", sql);
    }

    [Fact]
    public void GetFileLocationSql_Strips_Filename_For_Windows_And_Linux_Separators()
    {
        var sql = EventSql.GetFileLocationSql();

        Assert.Contains(@"PATINDEX('%[\/]%'", sql);
    }

    [Fact]
    public void GetCreateSessionSql_Includes_Spid_SessionName_Filter()
    {
        var sql = EventSql.GetCreateSessionSql("MySession", @"C:\Trace\MySession.xel", 52, false, new EventOptions());

        Assert.Contains("CREATE EVENT SESSION [MySession] ON SERVER", sql);
        Assert.Contains("sqlserver.session_id = 52", sql);
        Assert.Contains("sqlserver.sql_text NOT LIKE '%MySession%'", sql);
    }

    [Fact]
    public void GetCreateSessionSql_Adds_LogEvents_When_ReplayMode()
    {
        var options = new EventOptions { IncludeLockModeCategories = [], IncludeWait = false, IncludeMemory = false };

        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, true, options);

        var addEventCount = CountOccurrences(sql, "ADD EVENT ");

        Assert.Contains("sqlserver.transaction_log", sql);
    }

    [Fact]
    public void GetCreateSessionSql_Includes_Callstack_Action_Only_When_Requested()
    {
        var withCallstack = EventSql.GetCreateSessionSql(
            "Sess", @"C:\Trace\Sess.xel", 1, false, new EventOptions { IncludeCallStack = true });

        var withoutCallstack = EventSql.GetCreateSessionSql(
            "Sess", @"C:\Trace\Sess.xel", 1, false, new EventOptions { IncludeCallStack = false });

        Assert.Contains("package0.callstack", withCallstack);
        Assert.DoesNotContain("package0.callstack", withoutCallstack);
    }

    [Fact]
    public void GetCreateSessionSql_Captures_Only_Batch_And_Operator_Events_When_Recording_Time_Travel()
    {
        var options = new EventOptions { RecordTimeTravel = true, IncludeCallStack = true, IncludeLatch = true };

        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, false, options);

        var events = sql.Split('\n')
                        .Where(l => l.StartsWith("ADD EVENT ", StringComparison.Ordinal))
                        .Select(l => l["ADD EVENT ".Length..].Trim())
                        .ToList();

        Assert.Equal(EventConstants.TimeTravelEvents, events);
        Assert.Contains("opcode = 1 AND (wait_type = 'CXPACKET'", sql);
        Assert.Contains("sqlos.system_thread_id", sql);
        Assert.DoesNotContain("package0.callstack", sql);
    }

    [Fact]
    public void GetCreateSessionSql_Omits_System_Thread_Id_When_Not_Recording_Time_Travel()
    {
        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, false, new EventOptions());

        Assert.DoesNotContain("sqlos.system_thread_id", sql);
    }

    [Fact]
    public async Task GetEventKeyAddresses_With_No_Events_Completes_Without_Touching_Database()
    {
        var events = new List<EngineEvent>();

        await new EventReader(NullLogger<EventReader>.Instance).GetEventKeyAddresses(events, "irrelevant", null, CancellationToken.None);
    }

    [Fact]
    public async Task GetEventKeyAddresses_Skips_Lookup_When_No_AllocationUnit_Matches_ObjectId()
    {
        var lockEvent = new LockEvent { Resource = new LockResource { ObjectId = 42, KeyHash = "somehash" } };

        var events = new List<EngineEvent> { lockEvent };

        await new EventReader(NullLogger<EventReader>.Instance).GetEventKeyAddresses(events, "irrelevant", null, CancellationToken.None);

        Assert.Null(lockEvent.Resource.RowIdentifier);
    }

    [Fact]
    public async Task GetEventKeyAddresses_Ignores_LockEvents_Without_KeyHash()
    {
        var lockEvent = new LockEvent { Resource = new LockResource { ObjectId = 42, KeyHash = null } };

        var events = new List<EngineEvent> { lockEvent };

        await new EventReader(NullLogger<EventReader>.Instance).GetEventKeyAddresses(events, "irrelevant", null, CancellationToken.None);

        Assert.Null(lockEvent.Resource.RowIdentifier);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
