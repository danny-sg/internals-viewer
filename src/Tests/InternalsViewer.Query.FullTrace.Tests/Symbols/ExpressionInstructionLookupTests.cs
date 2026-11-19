using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class ExpressionInstructionLookupTests
{
    [Theory]
    [InlineData("SqlTsEs", "GetData(CEsExec *,CXVariant *)", true)]
    [InlineData("sqltses", "CValEsConstXVar::EsGetData8(CEsExec *,CXVariant *)", true)]
    [InlineData("SqlTsEs", "CEsExec::GeneralEval4(unsigned __int64 *,CEsExec *,CXVariant *)", false)]
    [InlineData("SqlTsEs", "CEsMDIntrinsic(CEsMDIntrinsicParams *,CDataArray *)", false)]
    [InlineData("sqlmin", "GetData(CEsExec *,CXVariant *)", false)]
    public void An_Instruction_Is_A_SqlTsEs_Function_Taking_An_Executor_And_A_Variant(string module, string signature, bool expected)
        => Assert.Equal(expected, ExpressionInstructionLookup.IsInstruction(module, signature));

    [Theory]
    [InlineData(-1, "GetData", true)]
    [InlineData(-1, "CValEsConstXVar::EsGetData8", true)]
    [InlineData(1, "ManipData", true)]
    [InlineData(3, "SetMultData", true)]
    [InlineData(1, "CTEsCompare<56,56>::BlCompareXcArgArg", false)]
    [InlineData(0, "ESJump", false)]
    [InlineData(1, null, false)]
    public void Loads_And_Stores_Name_An_Operand_By_Index(int count, string? handler, bool expected)
        => Assert.Equal(expected, ExpressionInstructionLookup.IsOperandAccess(count, handler));
}
