using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Tests;

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
    public void GetCreateSessionSql_Includes_Filename_And_Spid_Filter()
    {
        var sql = EventSql.GetCreateSessionSql("MySession", @"C:\Trace\MySession.xel", 52, false, new EventOptions());

        Assert.Contains("CREATE EVENT SESSION [MySession] ON SERVER", sql);
        Assert.Contains(@"filename = 'C:\Trace\MySession.xel'", sql);
        Assert.Contains("sqlserver.session_id = 52", sql);
        Assert.Contains("sqlserver.sql_text NOT LIKE '%LOG_READ_MySession%'", sql);
    }

    [Fact]
    public void GetCreateSessionSql_Has_One_AddEvent_Per_Base_Event_When_Not_Replay_And_No_Extras()
    {
        var options = new EventOptions { IncludeLock = false, IncludeWait = false, IncludeMemory = false };

        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, false, options);

        var addEventCount = CountOccurrences(sql, "ADD EVENT ");

        Assert.Equal(EventConstants.Events.Length, addEventCount);
    }

    [Fact]
    public void GetCreateSessionSql_Adds_LogEvents_When_ReplayMode()
    {
        var options = new EventOptions { IncludeLock = false, IncludeWait = false, IncludeMemory = false };

        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, true, options);

        var addEventCount = CountOccurrences(sql, "ADD EVENT ");

        Assert.Equal(EventConstants.Events.Length + EventConstants.LogEvents.Length, addEventCount);
        Assert.Contains("sqlserver.transaction_log", sql);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void GetCreateSessionSql_Adds_Optional_Event_Groups_When_Requested(bool includeLock,
                                                                              bool includeWait,
                                                                              bool includeMemory)
    {
        var options = new EventOptions
        {
            IncludeLock = includeLock,
            IncludeWait = includeWait,
            IncludeMemory = includeMemory
        };

        var sql = EventSql.GetCreateSessionSql("Sess", @"C:\Trace\Sess.xel", 1, false, options);

        var extraCount = (includeLock ? EventConstants.LockEvents.Length : 0)
                          + (includeWait ? EventConstants.WaitEvents.Length : 0)
                          + (includeMemory ? EventConstants.MemoryEvents.Length : 0);

        var addEventCount = CountOccurrences(sql, "ADD EVENT ");

        Assert.Equal(EventConstants.Events.Length + extraCount, addEventCount);
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
    public async Task GetEventKeyAddresses_With_No_Events_Completes_Without_Touching_Database()
    {
        var events = new List<EngineEvent>();

        await QueryRunner.GetEventKeyAddresses(events, new Dictionary<long, AllocationUnit>(), "irrelevant",
                                               CancellationToken.None);
    }

    [Fact]
    public async Task GetEventKeyAddresses_Skips_Lookup_When_No_AllocationUnit_Matches_ObjectId()
    {
        var lockEvent = new LockEvent { ObjectId = 42, KeyHash = "somehash" };

        var events = new List<EngineEvent> { lockEvent };

        await QueryRunner.GetEventKeyAddresses(events, new Dictionary<long, AllocationUnit>(), "irrelevant",
                                               CancellationToken.None);

        Assert.Null(lockEvent.RowIdentifier);
    }

    [Fact]
    public async Task GetEventKeyAddresses_Ignores_LockEvents_Without_KeyHash()
    {
        var lockEvent = new LockEvent { ObjectId = 42, KeyHash = null };

        var events = new List<EngineEvent> { lockEvent };

        var allocationUnits = new Dictionary<long, AllocationUnit> { [1] = new() { ObjectId = 42 } };

        await QueryRunner.GetEventKeyAddresses(events, allocationUnits, "irrelevant", CancellationToken.None);

        Assert.Null(lockEvent.RowIdentifier);
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
