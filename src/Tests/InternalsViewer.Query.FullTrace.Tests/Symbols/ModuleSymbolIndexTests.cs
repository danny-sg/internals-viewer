using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class ModuleSymbolIndexTests
{
    private const ulong Sqllang = 0x7FF9_8800_0000;

    private const ulong Sqlmin = 0x7FF9_9000_0000;

    private static readonly ModuleSymbolIndex Index = new([("sqllang", Sqllang, GetSqllangSymbols()),
                                                           ("sqlmin", Sqlmin, GetSqlminSymbols())]);

    [Theory]
    [InlineData(Sqllang + 0x1000, "sqllang!CCompPlan::Init")]
    [InlineData(Sqllang + 0x1010, "sqllang!CCompPlan::Init+0x10")]
    [InlineData(Sqllang + 0x5000, "sqllang!CCompPlan::`vftable'")]
    [InlineData(Sqllang + 0x5008, "sqllang!CCompPlan::`vftable'+0x8")]
    [InlineData(Sqllang + 0x5800, "sqllang+0x5800")]
    [InlineData(Sqlmin + 0x2000, "sqlmin!CQScanHash::GetRow")]
    public void An_Address_In_A_Module_Is_Named_By_The_Symbol_At_Or_Below_It(ulong value, string expected)
        => Assert.Equal(expected, Index.Find(value)?.ToString());

    [Fact]
    public void An_Address_Below_Every_Module_Is_Not_In_A_Module()
        => Assert.Null(Index.Find(0x0000_026F_0ABA_AC50));

    [Fact]
    public void An_Address_Far_Past_The_Last_Symbol_Is_Not_In_The_Module()
        => Assert.Null(Index.Find(Sqlmin + 0x100_0000));

    [Fact]
    public void A_Decorated_Name_Is_Only_Given_For_The_Exact_Start_Of_A_Symbol()
        => Assert.Equal(("??_7CCompPlan@@6B@", null), (Index.GetDecoratedName(Sqllang + 0x5000), Index.GetDecoratedName(Sqllang + 0x5008)));

    [Fact]
    public void A_String_Literal_Is_Recognised_Only_At_Its_Start()
        => Assert.Equal((true, false), (Index.Find(Sqllang + 0x6000)?.IsString, Index.Find(Sqllang + 0x6004)?.IsString));

    private static SymbolTable GetSqllangSymbols()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x1000, true, "CCompPlan::Init", "void CCompPlan::Init(void)", "?Init@CCompPlan@@QEAAXXZ");

        builder.Add(0x5000, false, "CCompPlan::`vftable'", string.Empty, "??_7CCompPlan@@6B@");

        builder.Add(0x6000, false, "`string'", string.Empty, "??_C@_0BC@ABCDEFGH@sql?2ntdbms@");

        return builder.Build();
    }

    private static SymbolTable GetSqlminSymbols()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x2000, true, "CQScanHash::GetRow", "unsigned char CQScanHash::GetRow(void)", "?GetRow@CQScanHash@@QEAAEXZ");

        return builder.Build();
    }
}
