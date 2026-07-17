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
        Assert.Equal(expected, CategoryMappings.Default.Classify(module: null, className, methodName));

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
        Assert.Equal(SymbolCategory.QueryOperator, CategoryMappings.Default.Classify("sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal("Top", CategoryMappings.Default.ClassifyOperator("sqlmin", "CQScanTopNew", "GetRow").Iterator);
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
    // "TableScan" names the access method, not the plan node: an unordered clustered-index scan comes through here.
    // Miss it and the Sort above swallows the scan whole, having nothing to trim at.
    [InlineData("CQScanTableScanNew", "GetRow", "Clustered Index Scan")]
    [InlineData("CQScanTableScanNew", "GetRow", "Table Scan")]
    public void Default_Recognises_An_Operator_Entry_Frame(string className, string methodName, string physicalOperator)
        => Assert.True(CategoryMappings.Default.ClassifyOperator("sqlmin", className, methodName)
                                       .PlanOperator.Any(p => p.Matches(physicalOperator)));

    [Theory]
    // Nothing the plan has a node for: a badge, but never a segment boundary.
    [InlineData("CQScanProfileNew", "GetRow")]
    [InlineData("RowsetNewSS", "FetchNextRow")]
    [InlineData("SomeUnknownClass", "SomeUnknownMethod")]
    public void Default_Marks_No_Entry_Frame_For_A_Non_Operator(string className, string methodName)
        => Assert.Empty(CategoryMappings.Default.ClassifyOperator(module: null, className, methodName).PlanOperator);

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
        => Assert.True(CategoryMappings.Default.ClassifyOperator("sqllang", className, "XretExecute")
                                       .PlanOperator.Any(p => p.Matches(statementType)));

    [Fact]
    public void An_Entry_Frame_Only_Matches_Its_Own_Operator()
    {
        var planOperator = CategoryMappings.Default.ClassifyOperator("sqlmin", "CQScanNLJoinNew", "GetRow").PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Nested Loops")));
        Assert.False(planOperator.Any(p => p.Matches("Merge Join")));
    }

    [Fact]
    public void Module_Pinned_Operator_Rule_Beats_Generic_Function_Rule()
    {
        // sqlmin|CQScanHash*|* outranks the generic *|*|GetRow* rule, which would otherwise make this RowAccess.
        Assert.Equal(SymbolCategory.PhysicalOperator,
                     CategoryMappings.Default.Classify("sqlmin", "CQScanHashNew", "GetRow"));
    }

    [Fact]
    public void Most_Specific_Rule_Wins()
    {
        var symbols = string.Join('\n',
            "*|*|*|RowAccess",                          // matches anything, lowest specificity
            "sqlmin|CQScan*|*|QueryOperator",           // module + class glob
            "sqlmin|CQScanTopNew|GetRow|Compilation");  // fully exact

        var mappings = CategoryMappings.Load(new StringReader(""), new StringReader(symbols));

        Assert.Equal(SymbolCategory.Compilation, mappings.Classify("sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal(SymbolCategory.QueryOperator, mappings.Classify("sqlmin", "CQScanHashNew", "GetRow"));
        Assert.Equal(SymbolCategory.RowAccess, mappings.Classify("other", "Whatever", "Anything"));
    }

    [Fact]
    public void A_Badge_And_A_Boundary_Are_Chosen_Independently()
    {
        // The whole point of splitting these out. The specific rule renames the badge to the phase and states no
        // boundary; the general one supplies it. Sharing SymbolCategories' single contest, the specific rule would win
        // outright and its blank cell would delete the boundary — which is why every rule used to repeat it.
        var operators = string.Join('\n',
            "sqlmin|CQScanHash*|*|Hash Match|Hash Match",
            "sqlmin|CQScanHash|ConsumeProbe|Hash Match Probe|");

        var mappings = CategoryMappings.Load(new StringReader(""),
                                             new StringReader(""),
                                             new StringReader(operators));

        var (iterator, planOperator) = mappings.ClassifyOperator("sqlmin", "CQScanHash", "ConsumeProbe");

        Assert.Equal("Hash Match Probe", iterator);
        Assert.True(planOperator.Any(p => p.Matches("Hash Match")));
    }


    [Fact]
    public void Override_Wins_An_Exact_Tie()
    {
        var core = "*|CQScanTop*|*|QueryOperator";
        var update = "*|CQScanTop*|*|Compilation";

        var mappings = CategoryMappings.Load(new StringReader(""),
                                             new StringReader(core),
                                             overrideSymbols: new StringReader(update));

        Assert.Equal(SymbolCategory.Compilation, mappings.Classify(null, "CQScanTopNew", "Foo"));
    }

    [Fact]
    public void Default_Recognises_An_Access_Barrier()
    {
        // Where a page fetch begins, and so where an individual read's own stack starts.
        Assert.True(CategoryMappings.Default.IsAccessBarrier("sqlmin", "BPool", "Get"));

        // GetFromDisk sits BELOW Get and is part of the same fetch. Marking it would win on nearest-above and cut the
        // read's stack in half, hiding the buffer-pool lookup that led to it.
        Assert.False(CategoryMappings.Default.IsAccessBarrier("sqlmin", "BPool", "GetFromDisk"));

        // The access methods that call it are above the barrier, not it: they are the operator's work, not the read's.
        Assert.False(CategoryMappings.Default.IsAccessBarrier("sqlmin", "IndexPageManager", "GetPageWithKey"));
        Assert.False(CategoryMappings.Default.IsAccessBarrier("sqlmin", "BTreeMgr", "Seek"));
    }

    [Fact]
    public void Barriers_Are_Absent_Rather_Than_Universal_When_Not_Loaded()
    {
        // The file is optional on Load, and an empty barrier set must match nothing — matching everything would cut
        // every event's stack at its own leaf.
        var mappings = CategoryMappings.Load(new StringReader(""), new StringReader("*|*|*|RowAccess"));

        Assert.False(mappings.IsAccessBarrier("sqlmin", "BPool", "Get"));
    }

    [Fact]
    public void Default_Mappings_Load_Without_Malformed_Rows()
    {
        // Every rule parsed to a real category and the anchor rows resolve — a smoke test the embedded file is intact.
        Assert.NotEmpty(CategoryMappings.Default.Rules);
        Assert.Equal(SymbolCategory.XEventInfrastructure,
                     CategoryMappings.Default.Classify(null, "GenericEventFoo", "Publish"));
    }
}
