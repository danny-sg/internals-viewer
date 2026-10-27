using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Tools.Queries;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools.Queries;

[Trait("Category", "Unit")]
public class EventToolsTests
{
    [Fact]
    public async Task Events_Lists_The_Top_Level_Events_In_Time_Order()
    {
        var text = await Tools().Events();

        Assert.Contains("7 events (times are ms from the query start", text);
        Assert.Contains("Id  Time", text);

        var lines = Lines(text);

        Assert.Contains(lines, l => l.StartsWith("0 +0.030") && l.Contains("ExecutionOperatorEvent"));
        Assert.Contains(lines, l => l.StartsWith("3 +0.090 0.500") && l.Contains("ReadEventGroup"));
        Assert.Contains(lines, l => l.StartsWith("6 +0.690") && l.Contains("ReadEventGroup"));
    }

    [Fact]
    public async Task Events_Filters_By_Type_Name_Page_Thread_And_Node()
    {
        var tools = Tools();

        Assert.Contains("1 events", await tools.Events(type: "WaitEvent"));
        Assert.Contains("1 events", await tools.Events(name: "latch_acquired"));
        Assert.Contains("1 events", await tools.Events(page: "1:337"));
        Assert.Contains("3 events", await tools.Events(page: "1:336"));
        Assert.Contains("1 events", await tools.Events(thread: 1));
        Assert.Contains("3 events", await tools.Events(node: 0));
        Assert.Contains("2 events", await tools.Events(grep: "^ReadEventGroup"));
    }

    [Fact]
    public async Task Events_Counts_By_Type_And_Name()
    {
        var text = await Tools().Events(counts: true);

        var lines = Lines(text);

        Assert.Equal("Type Name Count", lines[0]);
        Assert.Contains("ReadEventGroup Read 2", lines);
        Assert.Contains("ExecutionOperatorEvent SELECT 1", lines);
        Assert.Contains("ExecutionOperatorEvent Clustered Index Scan 1", lines);
    }

    [Fact]
    public async Task Events_Sorts_By_Duration_And_Pages()
    {
        var text = await Tools().Events(sort: "duration", limit: 1);

        var lines = Lines(text);

        Assert.Contains(lines, l => l.StartsWith("5 +0.190 2.000 1 WaitEvent"));
        Assert.DoesNotContain(lines, l => l.Contains("ReadEventGroup"));
        Assert.Contains("(+6 more; use offset and limit)", text);
    }

    [Fact]
    public async Task Events_Shows_One_Event_With_Its_Call_Stack()
    {
        var text = await Tools().Events(eventId: 3);

        Assert.StartsWith("[3] +0.090 ReadEventGroup", text);
        Assert.Contains("TopLevel: True  Plan node: 0", text);
        Assert.Contains("CallStack:", text);
        Assert.Contains("node 2: sqlmin!CQScanRowsetNew::GetRow < sqlmin!CQueryScan::GetRow", text);
    }

    [Fact]
    public async Task Events_Rejects_An_Unknown_Id()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().Events(eventId: 99));
    }

    [Fact]
    public async Task Reads_Totals_Disk_And_Buffer_Pool_Reads_Per_Object()
    {
        var text = await Tools().Reads(list: true);

        var lines = Lines(text);

        Assert.Equal("Object Disk reads Disk pages Disk distinct Buffer reads Buffer distinct All distinct Warm ReadAhead AllocPages " +
                     "Total ms",
                     lines[0]);
        Assert.Contains("(unknown object) 1 2 2 1 1 2 0 0 0 0.510", lines);
        Assert.Contains("2 page reads, 0 physical page reads, 0 file reads totalling 0 B", lines);
        Assert.Contains("3 +0.090 0.500 disk 1:336-337 (2) 0", lines);
        Assert.Contains("6 +0.690 0.010 buffer 1:336 0", lines);
    }

    [Fact]
    public async Task Waits_Groups_By_Wait_Type_And_Lists_The_Longest()
    {
        var text = await Tools().Waits(list: true);

        var lines = Lines(text);

        Assert.Equal("Wait type Count Total ms Max ms", lines[0]);
        Assert.Contains("LCK_M_S 1 2.000 2.000", lines);
        Assert.Contains("Id Time Dur ms Thr Wait type Page Object", lines);
        Assert.Contains("5 +0.190 2.000 1 LCK_M_S", lines);
    }

    [Fact]
    public async Task Locks_Groups_By_Resource_And_Mode_And_Lists_Each_Lock()
    {
        var text = await Tools().Locks(list: true);

        var lines = Lines(text);

        Assert.Equal("Resource Mode Event Object Count", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("Object S lock_acquired") && l.EndsWith(" 1"));
        Assert.Contains("Id Time Event Resource Mode Page/Key Xact Object", lines);
        Assert.Contains(lines, l => l.StartsWith("2 +0.050 lock_acquired Object S"));
        Assert.DoesNotContain("Lock groups:", text);
    }

    [Fact]
    public async Task Latches_Groups_By_Class_And_Mode_And_Counts_Pages()
    {
        var text = await Tools().Latches();

        var lines = Lines(text);

        Assert.Equal("Class Mode Event Count", lines[0]);
        Assert.Contains(lines, l => l.EndsWith(" SH latch_acquired 1"));
        Assert.Contains("1 distinct pages latched; most latched:", lines);
        Assert.Contains("1:336 1", lines);
    }

    [Fact]
    public async Task Columnstore_Reports_When_Nothing_Was_Captured()
    {
        var text = await Tools().Columnstore();

        Assert.Equal("No columnstore events were captured in this run.", text);
    }

    [Fact]
    public async Task Log_Records_Counts_By_Operation_And_Context()
    {
        var text = await Tools().LogRecords(list: true);

        Assert.StartsWith("0 log records", text);
        Assert.Contains("Operation  Context  Count", text);
    }

    private static EventTools Tools() => new(TestRuns.CreateResolver(TestRuns.Create()).Resolver);

    private static List<string> Lines(string text)
        => [.. text.Split('\n').Select(l => Regex.Replace(l.Trim(), " {2,}", " "))];
}
