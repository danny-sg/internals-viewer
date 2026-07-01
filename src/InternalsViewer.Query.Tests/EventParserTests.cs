using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Locks;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Tests;

public class EventParserTests
{
    private static EngineEvent? Parse(EventParser parser, string xml) => parser.ParseEvent(xml);

    [Fact]
    public void Parses_Name_Timestamp_And_Page_Fields()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        // page_location 0x100020000 => fileId 1, pageId 0x20000. The <type> metadata before <value>
        // must be skipped.
        var ev = Parse(parser,
            """
            <event name="page_read" package="sqlserver" timestamp="2026-06-30T12:00:00.123Z">
              <data name="page_location"><type name="uint64" package="package0" /><value>4295098368</value></data>
              <data name="type"><value>1</value></data>
            </event>
            """);

        var page = Assert.IsType<PageEvent>(ev);

        Assert.Equal("page_read", page.Name);
        Assert.Equal(1, page.PageAddress!.Value.FileId);
        Assert.Equal(0x20000, page.PageAddress.Value.PageId);
        Assert.Equal(123, page.Timestamp.Millisecond);
    }

    [Fact]
    public void Interns_Plan_Handle_To_Shared_Id()
    {
        var registry = new PlanHandleRegistry();
        var parser = new EventParser(null, registry);

        var a = Parse(parser, EventWithPlanHandle("0xAABB"));
        var b = Parse(parser, EventWithPlanHandle("0xAABB"));
        var c = Parse(parser, EventWithPlanHandle("0xCCDD"));

        Assert.NotEqual(PlanHandleRegistry.None, a!.PlanHandleId);
        Assert.Equal(a.PlanHandleId, b!.PlanHandleId);
        Assert.NotEqual(a.PlanHandleId, c!.PlanHandleId);
    }

    [Fact]
    public void Reused_Parser_Does_Not_Leak_Fields_Between_Events()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        // First event carries an object_id action; the second does not. The reused dictionaries must be
        // cleared so the second event doesn't inherit the first's object id.
        var first = Parse(parser,
            """
            <event name="lock_acquired" timestamp="2026-06-30T12:00:00.000Z">
              <data name="resource_type"><value>5</value></data>
              <data name="mode"><value>4</value></data>
              <data name="object_id"><value>42</value></data>
            </event>
            """);

        var second = Parse(parser,
            """
            <event name="lock_acquired" timestamp="2026-06-30T12:00:00.001Z">
              <data name="resource_type"><value>5</value></data>
              <data name="mode"><value>4</value></data>
            </event>
            """);

        Assert.Equal(42, first!.ObjectId);
        Assert.Equal(0, second!.ObjectId);
    }

    [Fact]
    public void Skips_Alter_Event_Session_Statements()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="sql_batch_completed" timestamp="2026-06-30T12:00:00.000Z">
              <action name="sql_text"><value>ALTER EVENT SESSION foo ON SERVER STATE = STOP</value></action>
            </event>
            """);

        Assert.Null(ev);
    }

    [Fact]
    public void Decodes_Xml_Entities_In_Values()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="sql_batch_starting" timestamp="2026-06-30T12:00:00.000Z">
              <data name="batch_text"><value>SELECT * FROM t WHERE a &lt; 1 &amp;&amp; b &gt; 2 &#x41;</value></data>
            </event>
            """);

        var batch = Assert.IsType<BatchStartEvent>(ev);

        Assert.Equal("SELECT * FROM t WHERE a < 1 && b > 2 A", batch.SqlText);
    }

    [Fact]
    public void Handles_Self_Closing_And_Missing_Values()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        // An empty self-closing <value/> and a <data/> with no value at all must not throw, and the event
        // is still produced.
        var ev = Parse(parser,
            """
            <event name="sql_batch_starting" timestamp="2026-06-30T12:00:00.000Z">
              <data name="batch_text"><value /></data>
              <data name="result" />
            </event>
            """);

        var batch = Assert.IsType<BatchStartEvent>(ev);

        Assert.Equal(string.Empty, batch.SqlText);
    }

    [Fact]
    public void Maps_Io_Event_From_File_Read()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="file_read_completed" timestamp="2026-06-30T12:00:00.000Z">
              <data name="file_id"><value>3</value></data>
              <data name="page_id"><value>42</value></data>
            </event>
            """);

        var io = Assert.IsType<IoEvent>(ev);

        Assert.True(io.IsRead);
        Assert.Equal(3, io.PageAddress!.Value.FileId);
        Assert.Equal(42, io.PageAddress.Value.PageId);
    }

    [Fact]
    public void Maps_Io_Event_PageId_From_Offset_When_Missing()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        // No page_id, so it is derived from offset / 8192.
        var ev = Parse(parser,
            """
            <event name="file_write_completed" timestamp="2026-06-30T12:00:00.000Z">
              <data name="file_id"><value>1</value></data>
              <data name="offset"><value>81920</value></data>
            </event>
            """);

        var io = Assert.IsType<IoEvent>(ev);

        Assert.False(io.IsRead);
        Assert.Equal(10, io.PageAddress!.Value.PageId);
    }

    [Fact]
    public void Maps_Wait_Event_With_Type_And_Duration()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="wait_completed" timestamp="2026-06-30T12:00:00.000Z">
              <data name="wait_type"><value>5</value></data>
              <data name="duration"><value>1234</value></data>
            </event>
            """);

        var wait = Assert.IsType<WaitEvent>(ev);

        Assert.Equal(WaitType.LCK_M_X, wait.WaitType);
        Assert.Equal(1234, wait.DurationUs);
    }

    [Fact]
    public void Maps_Page_Lock_To_Page_Address()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="lock_acquired" timestamp="2026-06-30T12:00:00.000Z">
              <data name="resource_type"><value>6</value></data>
              <data name="mode"><value>5</value></data>
              <data name="resource_0"><value>1</value></data>
              <data name="resource_1"><value>200</value></data>
            </event>
            """);

        var lockEvent = Assert.IsType<LockEvent>(ev);

        Assert.Equal(LockMode.X, lockEvent.LockMode);
        Assert.Equal(LockResourceType.Page, lockEvent.ResourceType);
        Assert.Equal(new PageAddress(1, 200), lockEvent.PageAddress);
    }

    [Fact]
    public void Maps_Rid_Lock_To_Row_Identifier()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="lock_acquired" timestamp="2026-06-30T12:00:00.000Z">
              <data name="resource_type"><value>9</value></data>
              <data name="mode"><value>5</value></data>
              <data name="resource_0"><value>1</value></data>
              <data name="resource_1"><value>200</value></data>
              <data name="resource_2"><value>3</value></data>
            </event>
            """);

        var lockEvent = Assert.IsType<LockEvent>(ev);

        Assert.NotNull(lockEvent.RowIdentifier);
        Assert.Equal(new PageAddress(1, 200), lockEvent.RowIdentifier!.PageAddress);
        Assert.Equal(3, lockEvent.RowIdentifier.SlotId);
    }

    [Fact]
    public void Maps_Key_Lock_To_Key_Hash()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="lock_acquired" timestamp="2026-06-30T12:00:00.000Z">
              <data name="resource_type"><value>7</value></data>
              <data name="mode"><value>5</value></data>
              <data name="resource_0"><value>255</value></data>
            </event>
            """);

        var lockEvent = Assert.IsType<LockEvent>(ev);

        Assert.Equal("(ff)", lockEvent.KeyHash);
    }

    [Fact]
    public void Maps_Transaction_Log_Event()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="transaction_log" timestamp="2026-06-30T12:00:00.000Z">
              <data name="operation"><value>2</value></data>
              <data name="context"><value>1</value></data>
              <data name="alloc_unit_id"><value>72057594038386688</value></data>
            </event>
            """);

        var logEvent = Assert.IsType<TransactionLogEvent>(ev);

        Assert.Equal(LogOperation.LOP_INSERT_ROWS, logEvent.Operation);
        Assert.Equal(72057594038386688, logEvent.AllocationUnitId);
    }

    [Fact]
    public void Maps_Memory_Event()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="query_memory_grant_usage" timestamp="2026-06-30T12:00:00.000Z">
              <data name="used_memory_kb"><value>512</value></data>
              <data name="granted_memory_kb"><value>1024</value></data>
            </event>
            """);

        var memory = Assert.IsType<MemoryEvent>(ev);

        Assert.Equal(512, memory.UsedMemoryKb);
        Assert.Equal(1024, memory.GrantedMemoryKb);
    }

    [Fact]
    public void Reads_Database_Id_From_Action()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="sql_batch_starting" timestamp="2026-06-30T12:00:00.000Z">
              <data name="batch_text"><value>SELECT 1</value></data>
              <action name="database_id"><value>7</value></action>
            </event>
            """);

        Assert.Equal(7, ev!.DatabaseId);
    }

    [Fact]
    public void Unknown_Event_Falls_Back_To_Base_Engine_Event()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser,
            """
            <event name="rpc_completed" timestamp="2026-06-30T12:00:00.000Z">
              <data name="duration"><value>99</value></data>
            </event>
            """);

        Assert.NotNull(ev);
        Assert.Equal("rpc_completed", ev!.Name);
        Assert.IsType<EngineEvent>(ev);
    }

    [Fact]
    public void Returns_Null_For_Missing_Event_Element()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser, "<not-an-event />");

        Assert.Null(ev);
    }

    [Fact]
    public void Returns_Null_When_Timestamp_Missing()
    {
        var parser = new EventParser(null, new PlanHandleRegistry());

        var ev = Parse(parser, """<event name="sql_batch_starting"></event>""");

        Assert.Null(ev);
    }

    private static string EventWithPlanHandle(string planHandle) =>
        $"""
         <event name="query_thread_profile" timestamp="2026-06-30T12:00:00.000Z">
           <data name="node_id"><value>1</value></data>
           <action name="plan_handle"><value>{planHandle}</value></action>
         </event>
         """;
}
