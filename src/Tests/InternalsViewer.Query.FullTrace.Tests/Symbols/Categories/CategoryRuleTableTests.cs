using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;


namespace InternalsViewer.Query.FullTrace.Tests.Symbols.Categories;

[Trait("Category", "Unit")]
public class CategoryRuleTableTests
{
    // Migrated from the old SymbolCategoryDictionaryTests, now resolved through the embedded default mappings.
    [Theory]
    [InlineData("CAutoQdsDBLock", "CreateTransactionAndAcquireQdsDBLock", SymbolCategory.QueryStore)]
    [InlineData("SOSHost_EventAuto", "Wait", SymbolCategory.Synchronisation)]
    [InlineData("CQDSManager", "SomeUncategorizedMethod", SymbolCategory.QueryStore)]
    [InlineData("SomeUnknownClass", "AcquireGenericQdsDbAndProcess", SymbolCategory.QueryStore)]
    [InlineData(null, "ExecuteCommandsInAutoTransaction", SymbolCategory.QueryExecution)]
    [InlineData("CSyncPoint", "Wait", SymbolCategory.Synchronisation)]
    [InlineData("CacheProbabilisticAlgorithm", "EntryReInitCost", SymbolCategory.SqlOs)]
    [InlineData("RbpCompile", "CompileFilter", SymbolCategory.ColumnStore)]
    [InlineData("RbpEarlyFilterTransform", "ReportFilterReplacedWithBitmapFilter", SymbolCategory.ColumnStore)]
    [InlineData("RbpExprFilter", "EvalImpure", SymbolCategory.ColumnStore)]
    [InlineData("RbpExprEvaluator", "Evaluate", SymbolCategory.ExpressionEvaluation)]
    [InlineData("RBPEXImpl", "ReadPages", SymbolCategory.BufferPool)]
    [InlineData("RbpexShrinkMgr", "Shrink", SymbolCategory.BufferPool)]
    [InlineData("CSIScanRowGroup", "GoDormant", SymbolCategory.ColumnStore)]
    [InlineData("VisibleHoBt", "GetColumnStorePrimaryDictionaryAttribute", SymbolCategory.Metadata)]
    [InlineData(null, "GetColumnStorePageCountsForQp", SymbolCategory.Metadata)]
    [InlineData("CMemo", "OptimizeQuery", SymbolCategory.Optimization)]
    [InlineData("CMIterStatsMemory", "GetNextStatForLeadingColumn", SymbolCategory.Optimization)]
    [InlineData("CVDManager", "CLoadDependentViews", SymbolCategory.QueryBinding)]
    [InlineData("CBpXte", "InitializeBatchIterators", SymbolCategory.QueryExecution)]
    [InlineData("SOS_Mutex", "Wait", SymbolCategory.Synchronisation)]
    [InlineData("SOS::Event<SOS::Events::AutoReset>", "ExecutePreWaitSteps", SymbolCategory.Synchronisation)]
    [InlineData("SOS_Task", "Sleep", SymbolCategory.Scheduling)]
    [InlineData("SOS_Scheduler", "SwitchContext", SymbolCategory.Scheduling)]
    [InlineData("WaitStats", "Record", SymbolCategory.SqlOs)]
    [InlineData("BUF", "AcquireLatch", SymbolCategory.BufferPool)]
    [InlineData("FCB", "Open", SymbolCategory.Storage)]
    [InlineData("CSession", "ReturnBlobHandleFactory", SymbolCategory.LargeObjectStorage)]
    [InlineData(null, "ConstructCXVariantRowAccessor", SymbolCategory.Rowset)]
    [InlineData("HeapDataSetSession", "GetNextRowValuesInternal", SymbolCategory.Rowset)]
    [InlineData("BTreeMgr", "Seek", SymbolCategory.BTreeNavigation)]
    [InlineData("CIDMgr", "Acquire", SymbolCategory.SqlOs)]
    [InlineData("CMemoryFeedbackMonitor", "CreateMemoryFeedbackInfoForRowMode", SymbolCategory.Memory)]
    [InlineData("CQueryResourceGrantManager", "GetGrant", SymbolCategory.Memory)]
    [InlineData("CMemObj", "Alloc", SymbolCategory.Memory)]
    [InlineData(null, "OperatorNew", SymbolCategory.Memory)]
    [InlineData(null, "operator new", SymbolCategory.Memory)]
    [InlineData(null, "operator==", SymbolCategory.System)]
    [InlineData("SomeUnknownClass", "SomeUnknownMethod", SymbolCategory.Unknown)]
    [InlineData(null, null, SymbolCategory.Unknown)]
    public void Default_Classifies(string? className, string? methodName, SymbolCategory expected) =>
        Assert.Equal(expected, CategoryRuleTable.Default.Classify(module: null, className, methodName));

    [Fact]
    public void Default_Modules_Resolve()
    {
        Assert.Equal(ModuleCategory.StorageEngine, CategoryRuleTable.Default.GetModuleCategory("sqlmin"));
        Assert.Equal(ModuleCategory.QueryProcessor, CategoryRuleTable.Default.GetModuleCategory("sqllang"));
        Assert.Equal(ModuleCategory.Unknown, CategoryRuleTable.Default.GetModuleCategory("nosuchmodule"));
    }

    [Fact]
    public void Operator_Iterator_Is_Resolved()
    {
        Assert.Equal(SymbolCategory.QueryOperator, CategoryRuleTable.Default.Classify("sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal("Top", CategoryRuleTable.Default.ClassifyOperator("sqlmin", "CQScanTopNew", "GetRow").OperatorName);
    }

    [Theory]
    // Stages inside a Columnstore Index Scan: a badge that names the phase, with no plan operator boundary of its own
    [InlineData("NormalColumnDataSet", "FetchNextColumnBatch", "Batch Iterator")]
    [InlineData("RowBucketScanner", "GetNextRowBucketAndFinalizeCurrent", "Row Buckets")]
    [InlineData("RowGroupManager", "GetNextRowGroup", "Rowgroups")]
    [InlineData("ColumnStoreObjectPool", "ConstructObjectAndFix", "Object Pool")]
    [InlineData("ColumnSegmentBuilder", "DeserializeFromDisk", "Segment Read")]
    [InlineData("ValueHashDictionaryBuilder", "DeserializeFromDisk", "Dictionary")]
    [InlineData("DeleteSegmentRef", "FindCurrentDeleteBitmap", "Delete Bitmap")]
    [InlineData("RbpCompile", "CompileFilter", "Compile")]
    public void Default_Badges_A_Columnstore_Scan_Stage(string className, string methodName, string expected)
    {
        var (iterator, planOperator) = CategoryRuleTable.Default.ClassifyOperator("sqlmin", className, methodName);

        Assert.Equal(expected, iterator);
        Assert.Empty(planOperator);
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
        => Assert.True(CategoryRuleTable.Default.ClassifyOperator("sqlmin", className, methodName)
                                       .PlanOperator.Any(p => p.Matches(physicalOperator)));

    [Theory]
    // Nothing the plan has a node for: a badge, but never a segment boundary.
    [InlineData("CQScanProfileNew", "GetRow")]
    [InlineData("RowsetNewSS", "FetchNextRow")]
    [InlineData("SomeUnknownClass", "SomeUnknownMethod")]
    public void Default_Marks_No_Entry_Frame_For_A_Non_Operator(string className, string methodName)
        => Assert.Empty(CategoryRuleTable.Default.ClassifyOperator(module: null, className, methodName).PlanOperator);

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
        => Assert.True(CategoryRuleTable.Default.ClassifyOperator("sqllang", className, "XretExecute")
                                       .PlanOperator.Any(p => p.Matches(statementType)));

    [Fact]
    public void An_Entry_Frame_Only_Matches_Its_Own_Operator()
    {
        var planOperator = CategoryRuleTable.Default.ClassifyOperator("sqlmin", "CQScanNLJoinNew", "GetRow").PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Nested Loops")));
        Assert.False(planOperator.Any(p => p.Matches("Merge Join")));
    }

    [Fact]
    public void Module_Pinned_Operator_Rule_Beats_Generic_Function_Rule()
    {
        // sqlmin|CQScanHash*|* outranks the generic *|*|GetRow* rule, which would otherwise make this RowAccess.
        Assert.Equal(SymbolCategory.QueryOperator,
                     CategoryRuleTable.Default.Classify("sqlmin", "CQScanHashNew", "GetRow"));
    }

    [Fact]
    public void Batch_Mode_Operators_Are_Batch_Operators()
        => Assert.Equal(SymbolCategory.BatchOperator, CategoryRuleTable.Default.Classify("sqlmin", "CBpQScanHashJoin", "GetBatch"));

    [Fact]
    public void Batches_Are_Part_Of_Batch_Operators()
        => Assert.Equal(SymbolCategory.BatchOperator, CategoryRuleTable.Default.Classify("sqlmin", "CBpBatch", "Reset"));

    [Fact]
    public void Most_Specific_Rule_Wins()
    {
        var symbols = string.Join('\n',
            "*|*|*|Rowset",                          // matches anything, lowest specificity
            "sqlmin|CQScan*|*|QueryOperator",           // module + class glob
            "sqlmin|CQScanTopNew|GetRow|Compilation");  // fully exact

        var mappings = CategoryRuleTable.Load(new StringReader(""), new StringReader(symbols));

        Assert.Equal(SymbolCategory.Compilation, mappings.Classify("sqlmin", "CQScanTopNew", "GetRow"));
        Assert.Equal(SymbolCategory.QueryOperator, mappings.Classify("sqlmin", "CQScanHashNew", "GetRow"));
        Assert.Equal(SymbolCategory.Rowset, mappings.Classify("other", "Whatever", "Anything"));
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

        var mappings = CategoryRuleTable.Load(new StringReader(""),
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

        var mappings = CategoryRuleTable.Load(new StringReader(""),
                                             new StringReader(core),
                                             overrideSymbols: new StringReader(update));

        Assert.Equal(SymbolCategory.Compilation, mappings.Classify(null, "CQScanTopNew", "Foo"));
    }

    [Fact]
    public void A_Fallback_Only_Applies_When_No_Rule_Matches()
    {
        var mappings = CategoryRuleTable.Load(new StringReader(""),
                                             new StringReader("SqlTsEs|CEsExec|*|ExpressionEvaluation"),
                                             fallbacks: new StringReader("SqlTsEs|*|*|Security"));

        Assert.Equal((SymbolCategory.ExpressionEvaluation, SymbolCategory.Security, SymbolCategory.Unknown),
                     (mappings.Classify("SqlTsEs", "CEsExec", "GeneralEval"),
                      mappings.Classify("SqlTsEs", "CUnmapped", "Run"),
                      mappings.Classify("sqlmin", "CUnmapped", "Run")));
    }

    [Fact]
    public void The_Most_Specific_Fallback_Wins()
    {
        var fallbacks = string.Join('\n', "SqlTsEs|*|*|ExpressionEvaluation", "SqlTsEs|*Enclave*|*|Security");

        var mappings = CategoryRuleTable.Load(new StringReader(""), new StringReader(""), fallbacks: new StringReader(fallbacks));

        Assert.Equal((SymbolCategory.Security, SymbolCategory.ExpressionEvaluation),
                     (mappings.Classify("SqlTsEs", "CSGXEnclave", "Call"), mappings.Classify("SqlTsEs", "CKLLSketch<long>", "Compress")));
    }

    [Fact]
    public void Default_Treats_Unmapped_Expression_Services_Symbols_As_Expression_Evaluation()
        => Assert.Equal((SymbolCategory.ExpressionEvaluation, SymbolCategory.Security),
                        (CategoryRuleTable.Default.Classify("SqlTsEs", null, "BlLikeStrStr"),
                         CategoryRuleTable.Default.Classify("SqlTsEs", "CEnclave", "CallEnclaveAsync")));

    [Fact]
    public void Default_Mappings_Load_Without_Malformed_Rows()
    {
        // Every rule parsed to a real category and the anchor rows resolve — a smoke test the embedded file is intact.
        Assert.NotEmpty(CategoryRuleTable.Default.Rules);
        Assert.Equal(SymbolCategory.XEventInfrastructure,
                     CategoryRuleTable.Default.Classify(null, "GenericEventFoo", "Publish"));
    }
}
