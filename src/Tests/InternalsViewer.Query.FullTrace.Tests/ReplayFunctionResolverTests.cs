using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

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
        => Assert.Equal(excluded, ReplayFunctionResolver.IsExcluded(category, iterator));

    [Theory]
    [InlineData("CQScanProfileNew", "GetRow")]
    [InlineData("CQScanProfileNew", "Open")]
    [InlineData("CQScanLightProfileNew", "GetRow")]
    public void A_Profile_Wrapper_Is_Tracing_But_Its_Calls_Stay_In_The_Replay(string className, string methodName)
    {
        var category = CategoryMappings.Default.Classify("sqlmin", className, methodName);

        var iterator = CategoryMappings.Default.ClassifyOperator("sqlmin", className, methodName).Iterator;

        Assert.Equal(SymbolCategory.Tracing, category);
        Assert.False(ReplayFunctionResolver.IsExcluded(category, iterator));
    }
}
