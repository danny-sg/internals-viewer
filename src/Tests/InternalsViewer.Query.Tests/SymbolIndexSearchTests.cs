using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.Tests;

public class SymbolIndexSearchTests
{
    [Fact]
    public void Search_Matches_Anywhere_In_The_Signature_Ignoring_Case()
    {
        var index = Build(("CQScanTableScanNew::GetRow", "int CQScanTableScanNew::GetRow(void)"),
                          ("HoBtAccess::AcquireHoBtRowGroupLock", "void HoBtAccess::AcquireHoBtRowGroupLock(unsigned long)"),
                          ("memcpy", ""));

        Assert.Equal([0x200u], index.Search("hobtrowgroup", SymbolSearchFields.All, 10).Select(h => h.Rva));
        Assert.Equal([0x100u, 0x200u], index.Search("ROW", SymbolSearchFields.None, 10).Select(h => h.Rva));
        Assert.Equal([0x300u], index.Search("memcpy", SymbolSearchFields.Signature, 10).Select(h => h.Rva));
    }

    [Fact]
    public void Class_Field_Matches_The_Class_Only_And_Signature_Field_Reaches_The_Parameters()
    {
        var index = Build(("Row::Fetch", "void Row::Fetch(PageId const &)"),
                          ("Page::Read", "int Page::Read(Row *)"));

        Assert.Equal([0x100u], index.Search("row", SymbolSearchFields.Class, 10).Select(h => h.Rva));
        Assert.Equal([0x100u, 0x200u], index.Search("row", SymbolSearchFields.Signature, 10).Select(h => h.Rva));
        Assert.Equal([0x100u], index.Search("pageid", SymbolSearchFields.Signature, 10).Select(h => h.Rva));
        Assert.Empty(index.Search("pageid", SymbolSearchFields.Class, 10));
    }

    [Fact]
    public void Search_Reports_Each_Symbol_Once_And_Stops_At_The_Limit()
    {
        var index = Build(("Row::RowRow", "Row::RowRow"), ("Rowset::Get", "Rowset::Get"), ("Other::Row", "Other::Row"));

        Assert.Equal([0x100u], index.Search("row", SymbolSearchFields.All, 1).Select(h => h.Rva));
        Assert.Equal([0x100u, 0x200u, 0x300u], index.Search("row", SymbolSearchFields.All, 10).Select(h => h.Rva));
    }

    [Fact]
    public void Search_Does_Not_Match_Across_Two_Adjacent_Entries()
    {
        var index = Build(("Alpha::EndsInAb", "Alpha::EndsInAb"), ("CdStart::Other", "CdStart::Other"));

        Assert.Empty(index.Search("abcd", SymbolSearchFields.All, 10));
        Assert.Empty(index.Search("alphacd", SymbolSearchFields.Class, 10));
    }

    [Fact]
    public void Build_Leaves_Out_Compiler_Generated_Names_And_First_Takes_From_The_Front()
    {
        var index = Build(("`anonymous namespace'::Helper", ""), ("Real::Function", ""), ("Real::Other", ""));

        Assert.Equal(2, index.Count);
        Assert.Empty(index.Search("   ", SymbolSearchFields.All, 10));
        Assert.Equal([0x200u], index.First(1).Select(h => h.Rva));
        Assert.True(index.Search("function", SymbolSearchFields.All, 10)[0].IsFunction);
    }

    [Fact]
    public void Wildcards_Match_The_Whole_Name_In_WinDbg_Style()
    {
        var index = Build(("XeSqlPkg::vector::Publish", "void XeSqlPkg::vector::Publish(unsigned int)"),
                          ("XeSqlPkg::vector::~vector", "XeSqlPkg::vector::~vector()"),
                          ("XeSqlPkg::Other::vector_get", "int XeSqlPkg::Other::vector_get()"),
                          ("CQScanTop::GetRow", "int CQScanTop::GetRow()"));

        Assert.Equal([0x100u, 0x200u], index.Search("*XeSqlPkg::vector*", SymbolSearchFields.All, 10).Select(h => h.Rva));
        Assert.Equal([0x100u, 0x200u], index.Search("XeSqlPkg::vector::*", SymbolSearchFields.Signature, 10).Select(h => h.Rva));
        Assert.Equal([0x400u], index.Search("*::GetRow", SymbolSearchFields.All, 10).Select(h => h.Rva));
        Assert.Equal([0x100u], index.Search("*Publish(unsigned ???)", SymbolSearchFields.Signature, 10).Select(h => h.Rva));
        Assert.Equal([0x100u, 0x200u], index.Search("xesqlpkg::vector", SymbolSearchFields.Class, 10).Select(h => h.Rva));
        Assert.Empty(index.Search("XeSqlPkg::vector", SymbolSearchFields.Class, 10).Where(h => h.Rva == 0x300u));
        Assert.Equal(4, index.Search("*", SymbolSearchFields.All, 10).Count);
        Assert.Empty(index.Search("GetRow", SymbolSearchFields.Class, 10));
    }

    [Fact]
    public void Matches_Rechecks_A_Described_Symbol_So_Folded_Neighbours_Are_Dropped()
    {
        const string vector = "XeSqlPkg::vector";

        const string publish = "XeSqlPkg::vector::Publish";

        const string publishSignature = "void XeSqlPkg::vector::Publish()";

        const string other = "CConnectionOutput";

        const string otherName = "CConnectionOutput::SendResetConnDoneImpl";

        Assert.True(SymbolIndex.Matches("vector", SymbolSearchFields.Class, vector, publish, publishSignature));
        Assert.False(SymbolIndex.Matches("vector", SymbolSearchFields.Class, other, otherName, $"void {otherName}()"));
        Assert.True(SymbolIndex.Matches("PageId", SymbolSearchFields.Signature, "Row", "Row::Fetch", "void Row::Fetch(PageId const &)"));
        Assert.False(SymbolIndex.Matches("PageId", SymbolSearchFields.Class, "Row", "Row::Fetch", "void Row::Fetch(PageId const &)"));
        Assert.True(SymbolIndex.Matches("XeSqlPkg::vector::*", SymbolSearchFields.None, vector, publish, publishSignature));
        Assert.False(SymbolIndex.Matches("XeSqlPkg::vector::*",
                                         SymbolSearchFields.None,
                                         "Other",
                                         "Other::Publish",
                                         "void Other::Publish()"));
        Assert.True(SymbolIndex.Matches("memcpy", SymbolSearchFields.All, string.Empty, "memcpy", string.Empty));
    }

    [Fact]
    public void A_Module_Prefix_Names_Where_To_Search_As_WinDbg_Writes_It()
    {
        Assert.Equal(("sqlmin", "CBpQScanColumnStoreScan::BpGetNextBatch"),
                     SymbolIndex.SplitModule("sqlmin!CBpQScanColumnStoreScan::BpGetNextBatch"));
        Assert.Equal((null, "GetRow"), SymbolIndex.SplitModule(" GetRow "));
        Assert.Equal(("sqlmin", ""), SymbolIndex.SplitModule("sqlmin!"));

        Assert.True(SymbolIndex.ModuleMatches("sqlmin", "sqlmin"));
        Assert.True(SymbolIndex.ModuleMatches("SQL", "sqlmin"));
        Assert.True(SymbolIndex.ModuleMatches("sql*", "sqllang"));
        Assert.False(SymbolIndex.ModuleMatches("sqlmin", "sqllang"));
        Assert.False(SymbolIndex.ModuleMatches("*min", "sqllang"));
    }

    private static SymbolIndex Build(params (string Name, string Signature)[] symbols) =>
        SymbolIndex.Build(symbols.Select((s, i) => new SymbolDetail(s.Name, s.Signature, (uint)(0x100 * (i + 1)), IsFunction: true)));
}
