using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Replay.Functions;

[Trait("Category", "Unit")]
public class ReplayFunctionResolverTests
{
    [Theory]
    [InlineData(SymbolCategory.XEventInfrastructure, null, true)]
    [InlineData(SymbolCategory.XEventInfrastructure, "Profiling", true)]
    [InlineData(SymbolCategory.Tracing, null, true)]
    [InlineData(SymbolCategory.Tracing, "Profiling", false)]
    [InlineData(SymbolCategory.QueryOperator, null, false)]
    public void Tracing_Is_Left_Out_Of_The_Replay_Unless_It_Is_An_Iterator(SymbolCategory category, string? iterator, bool excluded)
        => Assert.Equal(excluded, category.IsInstrumentation(iterator is not null));

    [Theory]
    [InlineData("CQScanProfileNew", "GetRow")]
    [InlineData("CQScanProfileNew", "Open")]
    [InlineData("CQScanLightProfileNew", "GetRow")]
    public void A_Profile_Wrapper_Is_A_Query_Operator_And_Its_Calls_Stay_In_The_Replay(string className, string methodName)
    {
        var category = CategoryRuleTable.Default.Classify("sqlmin", className, methodName);

        var iterator = CategoryRuleTable.Default.ClassifyOperator("sqlmin", className, methodName).OperatorName;

        Assert.Equal(SymbolCategory.QueryOperator, category);
        Assert.False(category.IsInstrumentation(iterator is not null));
    }

    [Theory]
    [InlineData("sqllang", "QueryHints", "AddHintXEventStringIfEnabled")]
    [InlineData("sqllang", "QueryHints", "FireXEventForHints")]
    [InlineData("sqllang", "IStatsCollection", "FireGuessXEvent")]
    [InlineData("sqllang", "CNormalizeExpr", "FireXEventForOpsInExpr")]
    [InlineData("sqllang", "BatchModeHeuristics", "FireHeuristicsXEvent")]
    [InlineData("sqldk", null, "XEventNoInline<`SOS_Task::PushWait'::`14'::<lambda_1> >")]
    public void A_Function_That_Fires_An_Extended_Event_Is_Cut(string module, string? className, string methodName)
    {
        var category = CategoryRuleTable.Default.Classify(module, className, methodName);

        Assert.True(category.IsInstrumentation(hasOperatorName: false));
    }
}
