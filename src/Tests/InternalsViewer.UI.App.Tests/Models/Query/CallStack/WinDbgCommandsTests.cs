using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class WinDbgCommandsTests
{
    [Fact]
    public void Symbol_Qualifies_The_Function_With_Its_Module_And_Drops_The_Offset()
    {
        var frame = Resolved("CQScanTableScanNew::GetRow+0x1a3");

        Assert.Equal("sqlmin!CQScanTableScanNew::GetRow", WinDbgCommands.Symbol(frame));
    }

    [Fact]
    public void Breakpoint_Targets_The_Function()
    {
        var frame = Resolved("CQScanTableScanNew::GetRow+0x1a3");

        Assert.Equal("bp sqlmin!CQScanTableScanNew::GetRow", WinDbgCommands.Breakpoint(frame));
        Assert.Equal("bp sqlmin!CQScanTableScanNew::GetRow \"k; g\"", WinDbgCommands.BreakpointWithStack(frame));
        Assert.Equal("x sqlmin!CQScanTableScanNew::GetRow", WinDbgCommands.ExamineSymbol(frame));
    }

    [Fact]
    public void BreakpointAtFrame_Adds_The_Offset_Captured_In_The_Frame()
    {
        var frame = Resolved("CQScanTableScanNew::GetRow+0x1a3");

        Assert.Equal("bp sqlmin!CQScanTableScanNew::GetRow+0x1A3", WinDbgCommands.BreakpointAtFrame(frame));
    }

    [Fact]
    public void Class_Commands_Target_The_Frames_Class()
    {
        var frame = Resolved("CQScanTableScanNew::GetRow+0x1a3");

        Assert.Equal("dt sqlmin!CQScanTableScanNew", WinDbgCommands.DisplayType(frame));
        Assert.Equal("x sqlmin!CQScanTableScanNew::*", WinDbgCommands.ListClassSymbols(frame));
    }

    [Fact]
    public void Class_Commands_Are_Null_For_A_Free_Function()
    {
        var frame = Resolved("SOS_Task::Param::Execute+0x20");

        Assert.NotNull(WinDbgCommands.DisplayType(frame));

        var free = Resolved("memcpy+0x40");

        Assert.Null(WinDbgCommands.DisplayType(free));
        Assert.Null(WinDbgCommands.ListClassSymbols(free));
    }

    [Fact]
    public void Template_Names_Are_Quoted_Where_The_Command_Parses_An_Expression()
    {
        var frame = Resolved("CQScanTop<1>::GetRow+0x10");

        Assert.Equal("bp @!\"sqlmin!CQScanTop<1>::GetRow\"", WinDbgCommands.Breakpoint(frame));
        Assert.Equal("bp @!\"sqlmin!CQScanTop<1>::GetRow\"+0x10", WinDbgCommands.BreakpointAtFrame(frame));
        Assert.Equal("bp @!\"sqlmin!CQScanTop<1>::GetRow\" \"k; g\"", WinDbgCommands.BreakpointWithStack(frame));
        Assert.Equal("x sqlmin!CQScanTop<1>::GetRow", WinDbgCommands.ExamineSymbol(frame));
        Assert.Equal("dt sqlmin!CQScanTop<1>", WinDbgCommands.DisplayType(frame));
    }

    [Fact]
    public void Unresolved_Frames_Are_Addressed_By_Module_Relative_Address()
    {
        var missingSymbols = new CallstackFrame { Module = "sqlmin", Rva = 0x2509D40 };

        Assert.Equal("sqlmin+0x2509D40", WinDbgCommands.Symbol(missingSymbols));
        Assert.Equal("bp sqlmin+0x2509D40", WinDbgCommands.Breakpoint(missingSymbols));
        Assert.Equal("bp sqlmin+0x2509D40", WinDbgCommands.BreakpointAtFrame(missingSymbols));
        Assert.Null(WinDbgCommands.DisplayType(missingSymbols));

        var unresolvedRva = Resolved("0x2509D40", 0x2509D40);

        Assert.Equal("bp sqlmin+0x2509D40", WinDbgCommands.Breakpoint(unresolvedRva));
        Assert.Null(WinDbgCommands.ListClassSymbols(unresolvedRva));
    }

    [Fact]
    public void Member_Commands_Restore_The_Class_And_Drop_The_Parameters()
    {
        var member = Member("AcquireHoBtRowGroupLock", "AcquireHoBtRowGroupLock(unsigned long,AutoHoBtRowGroupFlushLock *)");

        Assert.Equal("sqlmin!HoBtAccess::AcquireHoBtRowGroupLock", WinDbgCommands.Symbol(member));
        Assert.Equal("bp sqlmin!HoBtAccess::AcquireHoBtRowGroupLock", WinDbgCommands.Breakpoint(member));
        Assert.Equal("bp sqlmin!HoBtAccess::AcquireHoBtRowGroupLock \"k; g\"", WinDbgCommands.BreakpointWithStack(member));
        Assert.Equal("x sqlmin!HoBtAccess::AcquireHoBtRowGroupLock", WinDbgCommands.ExamineSymbol(member));
        Assert.Equal("dt sqlmin!HoBtAccess", WinDbgCommands.DisplayType(member));
        Assert.Equal("x sqlmin!HoBtAccess::*", WinDbgCommands.ListClassSymbols(member));
    }

    [Fact]
    public void Overloaded_Member_Breaks_By_Address_So_The_Chosen_Overload_Is_The_One_Hit()
    {
        var member = Member("GetRow", "GetRow(void)", rva: 0x2509D40, overloaded: true);

        Assert.Equal("sqlmin!HoBtAccess::GetRow", WinDbgCommands.Symbol(member));
        Assert.Equal("bp sqlmin+0x2509D40", WinDbgCommands.Breakpoint(member));
        Assert.Equal("bp sqlmin+0x2509D40 \"k; g\"", WinDbgCommands.BreakpointWithStack(member));
        Assert.Equal("bm sqlmin!HoBtAccess::GetRow", WinDbgCommands.BreakpointOnAllOverloads(member));
        Assert.Equal("x sqlmin!HoBtAccess::GetRow", WinDbgCommands.ExamineSymbol(member));
    }

    [Fact]
    public void DumpArguments_Reads_Each_Argument_From_The_Signature_One_Per_Line()
    {
        var member = Member("AcquireHoBtRowGroupLock", "AcquireHoBtRowGroupLock(unsigned long,AutoHoBtRowGroupFlushLock *)");

        var command = WinDbgCommands.DumpArguments(member);

        Assert.StartsWith("bp sqlmin!HoBtAccess::AcquireHoBtRowGroupLock \".echo === Arguments ===;", command);
        Assert.Contains(".printf \\\"this = rcx = %p\\\\n\\\", @rcx", command);
        Assert.Contains(".printf \\\"arg1 (unsigned long) = rdx = %p\\\\n\\\", @rdx", command);
        Assert.Contains(".printf \\\"arg2 (AutoHoBtRowGroupFlushLock *) = r8 = %p\\\\n\\\", @r8", command);
        Assert.EndsWith("; g\"", command);
    }

    [Fact]
    public void DumpArguments_Maps_Strings_And_Later_Arguments_To_The_Right_Location()
    {
        var member = Member("Probe", "Probe(char *,wchar_t *,int,int,int)");

        var command = WinDbgCommands.DumpArguments(member);

        Assert.Contains(".printf \\\"arg1 (char *) = rdx = %ma\\\\n\\\", @rdx", command);
        Assert.Contains(".printf \\\"arg2 (wchar_t *) = r8 = %mu\\\\n\\\", @r8", command);
        Assert.Contains(".printf \\\"arg3 (int) = r9 = %p\\\\n\\\", @r9", command);
        Assert.Contains(".printf \\\"arg4 (int) = [rsp+0x28] = %p\\\\n\\\", poi(@rsp+0x28)", command);
    }

    [Fact]
    public void DumpArgumentsAndBreak_Leaves_Out_The_Resume()
    {
        var member = Member("AcquireHoBtRowGroupLock", "AcquireHoBtRowGroupLock(unsigned long)");

        Assert.DoesNotContain("; g\"", WinDbgCommands.DumpArgumentsAndBreak(member));
    }

    private static ClassMemberRow Member(string name, string signature, uint rva = 0x1000, bool overloaded = false) =>
        new(string.Empty, new ClassMember("sqlmin", name, signature, rva, IsFunction: true), "HoBtAccess", overloaded);

    private static CallstackFrame Resolved(string symbol, uint rva = 0x1000) =>
        new()
        {
            Module = "sqlmin",
            Rva = rva,
            Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
        };
}
