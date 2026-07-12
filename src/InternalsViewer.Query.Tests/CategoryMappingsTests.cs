using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Tests;

public class CategoryMappingsTests
{
    // Migrated from the old SymbolCategoryDictionaryTests, now resolved through the embedded default mappings.
    [Theory]
    [InlineData("CAutoQdsDBLock", "CreateTransactionAndAcquireQdsDBLock", SymbolCategory.QueryStore)]
    [InlineData("SOSHost_EventAuto", "Wait", SymbolCategory.Scheduling)]
    [InlineData("CQDSManager", "SomeUncategorizedMethod", SymbolCategory.QueryStore)]
    [InlineData("SomeUnknownClass", "AcquireGenericQdsDbAndProcess", SymbolCategory.QueryStore)]
    [InlineData(null, "ExecuteCommandsInAutoTransaction", SymbolCategory.QueryExecution)]
    [InlineData("SomeUnknownClass", "SomeUnknownMethod", SymbolCategory.Unknown)]
    [InlineData(null, null, SymbolCategory.Unknown)]
    public void Default_Classifies(string? className, string? methodName, SymbolCategory expected) =>
        Assert.Equal(expected, CategoryMappings.Default.Classify(module: null, className, methodName).Category);

    [Fact]
    public void Default_Modules_Resolve()
    {
        Assert.Equal(ModuleCategory.StorageEngine, CategoryMappings.Default.GetModuleCategory("sqlmin"));
        Assert.Equal(ModuleCategory.QueryProcessor, CategoryMappings.Default.GetModuleCategory("sqllang"));
        Assert.Equal(ModuleCategory.Unknown, CategoryMappings.Default.GetModuleCategory("nosuchmodule"));
    }

    [Fact]
    public void Operator_Iterator_Is_Resolved()
    {
        var (category, iterator) = CategoryMappings.Default.Classify("sqlmin", "CQScanTopNew", "GetRow");

        Assert.Equal(SymbolCategory.QueryOperator, category);
        Assert.Equal("Top", iterator);
    }

    [Fact]
    public void Module_Pinned_Operator_Rule_Beats_Generic_Function_Rule()
    {
        // sqlmin|CQScanHash*|... outranks the generic *|*|GetRow* rule for a CQScan GetRow frame.
        Assert.Equal(SymbolCategory.QueryOperator,
                     CategoryMappings.Default.Classify("sqlmin", "CQScanHashNew", "GetRow").Category);
    }

    [Fact]
    public void Most_Specific_Rule_Wins()
    {
        var symbols = string.Join('\n',
            "*|*|*|RowAccess|",                             // matches anything, lowest specificity
            "sqlmin|CQScan*|*|QueryOperator|Op",            // module + class glob
            "sqlmin|CQScanTopNew|GetRow|Compilation|Top");  // fully exact

        var mappings = CategoryMappings.Load(new StringReader(""), new StringReader(symbols));

        Assert.Equal((SymbolCategory.Compilation, "Top"), mappings.Classify("sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal((SymbolCategory.QueryOperator, "Op"), mappings.Classify("sqlmin", "CQScanHashNew", "GetRow"));
        Assert.Equal((SymbolCategory.RowAccess, (string?)null), mappings.Classify("other", "Whatever", "Anything"));
    }

    [Fact]
    public void Override_Wins_An_Exact_Tie()
    {
        var core = "*|CQScanTop*|*|QueryOperator|Top";
        var update = "*|CQScanTop*|*|Compilation|Overridden";

        var mappings = CategoryMappings.Load(new StringReader(""),
                                             new StringReader(core),
                                             overrideSymbols: new StringReader(update));

        Assert.Equal((SymbolCategory.Compilation, "Overridden"), mappings.Classify(null, "CQScanTopNew", "Foo"));
    }

    [Fact]
    public void Default_Mappings_Load_Without_Malformed_Rows()
    {
        // Every rule parsed to a real category and the anchor rows resolve — a smoke test the embedded file is intact.
        Assert.NotEmpty(CategoryMappings.Default.Rules);
        Assert.Equal(SymbolCategory.XEventInfrastructure,
                     CategoryMappings.Default.Classify(null, "GenericEventFoo", "Publish").Category);
    }
}
