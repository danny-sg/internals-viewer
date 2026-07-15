using InternalsViewer.Query.CallStack.Categories;

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
        var (category, iterator, _) = CategoryMappings.Default.Classify("sqlmin", "CQScanTopNew", "GetRow");

        Assert.Equal(SymbolCategory.QueryOperator, category);
        Assert.Equal("Top", iterator);
    }

    [Theory]
    // The badge names the phase, but all three CQScanHash rules are entries to the one Hash Match plan node.
    [InlineData("CQScanHashNew", "GetRow", "Hash Match")]
    [InlineData("CQScanHash", "ConsumeBuild", "Hash Match")]
    // One class, several plan operators. The Clustered variants the plan names separately, and the lookups, which
    // ExecutionPlanParser renames off the same seek — nothing about "Key Lookup" resembles "Index Seek", so this only
    // works because the cell takes a list. Miss it and the loop above cannot trim that input away either.
    [InlineData("CQScanRangeNew", "EvalSetRangeExpr", "Index Seek")]
    [InlineData("CQScanRangeNew", "EvalSetRangeExpr", "Clustered Index Seek")]
    [InlineData("CQScanRangeNew", "EvalSetRangeExpr", "Key Lookup")]
    [InlineData("CQScanRangeNew", "EvalSetRangeExpr", "RID Lookup")]
    // The real frames a nested loop and its inputs are entered through, read off a captured stack. The rowset iterator
    // streams a seek's range (FetchNextRow), not only a scan's — miss that and the loop above swallows the whole input.
    [InlineData("CQScanNLJoinTrivialNew", "GetRow", "Nested Loops")]
    [InlineData("CQScanStreamAggregateNew", "GetCalculatedRow", "Stream Aggregate")]
    [InlineData("CQScanRowsetNew", "GetRowWithPrefetch", "Index Seek")]
    [InlineData("CQScanRowsetNew", "GetRowWithPrefetch", "Clustered Index Seek")]
    [InlineData("CQScanRowsetNew", "GetRowWithPrefetch", "Index Scan")]
    public void Default_Recognises_An_Operator_Entry_Frame(string className, string methodName, string physicalOperator)
        => Assert.True(CategoryMappings.Default.Classify("sqlmin", className, methodName)
                                       .PlanOperator.Any(p => p.Matches(physicalOperator)));

    [Theory]
    // Nothing the plan has a node for: a badge, but never a segment boundary.
    [InlineData("CQScanProfileNew", "GetRow")]
    [InlineData("RowsetNewSS", "FetchNextRow")]
    [InlineData("SomeUnknownClass", "SomeUnknownMethod")]
    public void Default_Marks_No_Entry_Frame_For_A_Non_Operator(string className, string methodName)
        => Assert.Empty(CategoryMappings.Default.Classify(module: null, className, methodName).PlanOperator);

    [Theory]
    // A statement IS a plan node — ExecutionPlanParser.BuildStatementNode synthesises one (NodeId -1) whose
    // PhysicalOperator is showplan's StatementType. These names must stay in step with it, or the statement silently
    // stops scoping and its segment reverts to the full path from the thread start.
    [InlineData("CXStmtSelect", "SELECT")]
    [InlineData("CXStmtUpdate", "UPDATE")]
    [InlineData("CXStmtDelete", "DELETE")]
    [InlineData("CXStmtInsert", "INSERT")]
    [InlineData("CXStmtMerge", "MERGE")]
    public void Default_Scopes_A_Statement_To_Its_Plan_Node(string className, string statementType)
        => Assert.True(CategoryMappings.Default.Classify("sqllang", className, "XretExecute")
                                       .PlanOperator.Any(p => p.Matches(statementType)));

    [Fact]
    public void An_Entry_Frame_Only_Matches_Its_Own_Operator()
    {
        var planOperator = CategoryMappings.Default.Classify("sqlmin", "CQScanNLJoinNew", "GetRow").PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Nested Loops")));
        Assert.False(planOperator.Any(p => p.Matches("Merge Join")));
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

        Assert.Equal((SymbolCategory.Compilation, "Top"), Classified(mappings, "sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal((SymbolCategory.QueryOperator, "Op"), Classified(mappings, "sqlmin", "CQScanHashNew", "GetRow"));
        Assert.Equal((SymbolCategory.RowAccess, (string?)null), Classified(mappings, "other", "Whatever", "Anything"));
    }

    private static (SymbolCategory, string?) Classified(CategoryMappings mappings,
                                                        string? module,
                                                        string? className,
                                                        string? methodName)
    {
        var (category, iterator, _) = mappings.Classify(module, className, methodName);

        return (category, iterator);
    }

    [Fact]
    public void Override_Wins_An_Exact_Tie()
    {
        var core = "*|CQScanTop*|*|QueryOperator|Top";
        var update = "*|CQScanTop*|*|Compilation|Overridden";

        var mappings = CategoryMappings.Load(new StringReader(""),
                                             new StringReader(core),
                                             overrideSymbols: new StringReader(update));

        Assert.Equal((SymbolCategory.Compilation, "Overridden"), Classified(mappings, null, "CQScanTopNew", "Foo"));
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
