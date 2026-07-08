using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Tests;

public class SymbolCategoryDictionaryTests
{
    [Theory]
    [InlineData("CAutoQdsDBLock", "CreateTransactionAndAcquireQdsDBLock")]
    [InlineData("CAutoQdsDBLock", "ReleaseQdsDBLock")]
    public void ClassName_With_Mixed_Case_Qds_Falls_Back_To_QueryStore(string className, string methodName)
    {
        var result = SymbolCategoryDictionary.GetCategory(className, methodName);

        Assert.Equal(SymbolCategory.QueryStore, result);
    }

    [Fact]
    public void SOSHost_EventAuto_ClassName_Resolves_To_Scheduling()
    {
        var result = SymbolCategoryDictionary.GetCategory("SOSHost_EventAuto", "Wait");

        Assert.Equal(SymbolCategory.Scheduling, result);
    }

    [Fact]
    public void Exact_ClassCategory_Match_Takes_Priority_Over_Method_Lookup()
    {
        var result = SymbolCategoryDictionary.GetCategory("CQDSManager", "SomeUncategorizedMethod");

        Assert.Equal(SymbolCategory.QueryStore, result);
    }

    [Fact]
    public void Falls_Back_To_MethodCategory_When_ClassName_Is_Unrecognized()
    {
        var result = SymbolCategoryDictionary.GetCategory("SomeUnknownClass", "AcquireGenericQdsDbAndProcess");

        Assert.Equal(SymbolCategory.QueryStore, result);
    }

    [Fact]
    public void Falls_Back_To_MethodCategory_When_ClassName_Is_Null()
    {
        var result = SymbolCategoryDictionary.GetCategory(null, "ExecuteCommandsInAutoTransaction");

        Assert.Equal(SymbolCategory.QueryExecution, result);
    }

    [Fact]
    public void Unrecognized_ClassName_And_MethodName_Returns_Unknown()
    {
        var result = SymbolCategoryDictionary.GetCategory("SomeUnknownClass", "SomeUnknownMethod");

        Assert.Equal(SymbolCategory.Unknown, result);
    }

    [Fact]
    public void Null_ClassName_And_MethodName_Returns_Unknown()
    {
        var result = SymbolCategoryDictionary.GetCategory(null, null);

        Assert.Equal(SymbolCategory.Unknown, result);
    }
}
