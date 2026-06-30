using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;

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

    private static string EventWithPlanHandle(string planHandle) =>
        $"""
         <event name="query_thread_profile" timestamp="2026-06-30T12:00:00.000Z">
           <data name="node_id"><value>1</value></data>
           <action name="plan_handle"><value>{planHandle}</value></action>
         </event>
         """;
}
