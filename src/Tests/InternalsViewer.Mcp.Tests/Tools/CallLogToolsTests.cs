using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Tools;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class CallLogToolsTests
{
    private const string GetRow = "sqlmin!CQScanRowsetNew::GetRow";

    [Fact]
    public async Task Calls_Lists_The_Logged_Functions_With_Their_Counts()
    {
        var text = await Tools().Calls();

        var lines = Lines(text);

        Assert.Equal("1 (function, instance) call lists in the call log, 2 calls in the 1 listed", lines[0]);
        Assert.Equal(["Function", "Address", "Instance", "Calls", "Locators"], Cells(lines[1]));
        Assert.Equal([GetRow, "0x30", "0x9000", "2", "1"], Cells(lines[3]));
    }

    [Fact]
    public async Task Calls_Filters_The_Function_List_By_Regex()
    {
        var text = await Tools().Calls(grep: "NoSuchFunction");

        var lines = Lines(text);

        Assert.Equal("1 (function, instance) call lists in the call log, 0 calls in the 0 listed", lines[0]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task Calls_Dumps_A_Functions_Calls_With_Arguments_And_Locators()
    {
        var text = await Tools().Calls(function: "GetRow");

        var lines = Lines(text);

        Assert.Equal($"{GetRow} address 0x30 instance 0x9000: 2 calls", lines[0]);
        Assert.Equal($"  #0 thread 0 1:0 to Unknown RCX=0x5 RDX=0x0 R8=0x0 R9=0x0 (no return)  in {GetRow}", lines[1]);
        Assert.Equal($"  #1 thread 0 2:0 to Unknown RCX=0x6 RDX=0x0 R8=0x0 R9=0x0 (no return)  in {GetRow}", lines[2]);
        Assert.Equal("      R8 page (1:336)", lines[3]);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public async Task Calls_Narrows_To_An_Instance()
    {
        Assert.Equal("no function in the call log matches", await Tools().Calls(function: "GetRow", instance: "0x1234"));

        Assert.Contains(": 2 calls", await Tools().Calls(function: "GetRow", instance: "9000"));
    }

    [Fact]
    public async Task Calls_Filters_By_A_Register_Value()
    {
        var text = await Tools().Calls(function: "GetRow", value: "6");

        var lines = Lines(text);

        Assert.StartsWith("  #1 ", lines[1]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task Calls_Filters_By_Thread()
    {
        var text = await Tools().Calls(function: "GetRow", thread: 9);

        Assert.Single(Lines(text));
    }

    [Fact]
    public async Task Calls_Pages_With_Offset_And_Limit()
    {
        var limited = Lines(await Tools().Calls(function: "GetRow", limit: 1));

        Assert.StartsWith("  #0 ", limited[1]);
        Assert.Equal("  (stopped after 1 calls; use offset or raise limit)", limited[2]);

        var offset = Lines(await Tools().Calls(function: "GetRow", offset: 1));

        Assert.StartsWith("  #1 ", offset[1]);
    }

    [Fact]
    public async Task Calls_Reports_No_Matching_Function()
    {
        Assert.Equal("no function in the call log matches", await Tools().Calls(function: "NoSuchFunction"));
    }

    [Fact]
    public async Task Calls_Needs_A_Full_Trace()
    {
        await Assert.ThrowsAsync<McpException>(() => Tools(withTrace: false).Calls());
    }

    private static CallLogTools Tools(bool withTrace = true)
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create(withTrace));

        return new CallLogTools(resolver);
    }

    private static string[] Lines(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static string[] Cells(string line) => Regex.Split(line.Trim(), @"\s{2,}");
}
