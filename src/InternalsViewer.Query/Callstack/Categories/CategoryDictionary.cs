namespace InternalsViewer.Query.Callstack.Categories;

internal class SymbolCategoryDictionary
{
    private static readonly Dictionary<string, SymbolCategory> ClassCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GenericEvent"] = SymbolCategory.XEventInfrastructure,
            ["XeSqlPkg"] = SymbolCategory.XEventInfrastructure,
            ["XEvent"] = SymbolCategory.XEventInfrastructure,
            ["CTraceData"] = SymbolCategory.XEventInfrastructure,
            ["lck_ProduceExtendedEvent"] = SymbolCategory.XEventInfrastructure,
            ["CProfileInfo"] = SymbolCategory.XEventInfrastructure,
            ["XE_"] = SymbolCategory.XEventInfrastructure,
            ["XeSosPkg"] = SymbolCategory.XEventInfrastructure,
            ["PublishWaitStatsXEvents"] = SymbolCategory.XEventInfrastructure,

            ["SOS_Task"] = SymbolCategory.SqlOs,
            ["SOS_Scheduler"] = SymbolCategory.SqlOs,
            ["Worker"] = SymbolCategory.SqlOs,
            ["ThreadScheduler"] = SymbolCategory.SqlOs,
            ["SchedulerManager"] = SymbolCategory.SqlOs,

            ["IOQueue"] = SymbolCategory.IoInfrastructure,
            ["DiskReadAsync"] = SymbolCategory.IoInfrastructure,
            ["JoinedIoCompletion"] = SymbolCategory.IoInfrastructure,
            ["WaitOnWriteAsyncToFinish"] = SymbolCategory.IoInfrastructure,
            ["write_data"] = SymbolCategory.IoInfrastructure,
            ["flush_buffer"] = SymbolCategory.IoInfrastructure,

            ["CMsqlExecContext"] = SymbolCategory.QueryExecution,
            ["CSQLSource"] = SymbolCategory.QueryExecution,
            ["CXStmt"] = SymbolCategory.QueryExecution,
            ["CLanguageExecEnv"] = SymbolCategory.QueryExecution,
            ["CExecuteStatement"] = SymbolCategory.QueryExecution,

            ["CQScan"] = SymbolCategory.QueryOperator,
            ["QueryScan"] = SymbolCategory.QueryOperator,
            ["CQueryScan"] = SymbolCategory.QueryOperator,

            ["BTree"] = SymbolCategory.IndexAccess,
            ["IndexPageManager"] = SymbolCategory.IndexAccess,

            ["BUF"] = SymbolCategory.BufferManager,
            ["BPool"] = SymbolCategory.BufferPool,
            ["FCB"] = SymbolCategory.FileControlBlock,

            ["Rowset"] = SymbolCategory.RowAccess,
            ["IndexRowScanner"] = SymbolCategory.RowAccess,
            ["CValFetchByKey"] = SymbolCategory.RowAccess,
            ["OpenRowset"] = SymbolCategory.RowAccess,
            ["OpenSystemTableRowset"] = SymbolCategory.RowAccess,
            ["FsInternalTableAccess"] = SymbolCategory.RowAccess,
            ["GetRowForKeyValue"] = SymbolCategory.RowAccess,

            ["DataSet"] = SymbolCategory.Dataset,
            ["IndexDataSetSession"] = SymbolCategory.Dataset,
            ["HeapDataSetSession"] = SymbolCategory.Dataset,

            ["FixPage"] = SymbolCategory.PageAccess,
            ["Page"] = SymbolCategory.PageAccess,
            ["HeapPageManager"] = SymbolCategory.PageAccess,

            ["Latch"] = SymbolCategory.Latching,
            ["Lock"] = SymbolCategory.Locking,
            ["lck_"] = SymbolCategory.Locking,
            ["MDL"] = SymbolCategory.Locking,
            ["CSQLLock"] = SymbolCategory.Locking,
            ["GetDataLock"] = SymbolCategory.Locking,
            ["GetHoBtLock"] = SymbolCategory.Locking,
            ["LockAndCheckState"] = SymbolCategory.Locking,
            ["AutoLockedHoBt"] = SymbolCategory.Locking,
            ["AcquireLock"] = SymbolCategory.Locking,
            ["SMD"] = SymbolCategory.Locking,

            ["WaitableBase"] = SymbolCategory.Scheduling,
            ["SOSHost_EventAuto"] = SymbolCategory.Scheduling,

            ["CStmtPrepQuery"] = SymbolCategory.StatementExecution,
            ["CExecStmtLoopVars"] = SymbolCategory.StatementExecution,

            ["CBatchTraceHelper"] = SymbolCategory.Tracing,
            ["EvGet"] = SymbolCategory.Tracing,
            ["PostEventSessionEvent"] = SymbolCategory.Tracing,
            ["CMIterTriggers"] = SymbolCategory.Tracing,

            ["SystemThreadDispatcher"] = SymbolCategory.WorkerManagement,
            ["CEsExec"] = SymbolCategory.ExpressionEvaluation,

            ["Blob"] = SymbolCategory.LargeObjectStorage,
            ["BlobBase"] = SymbolCategory.LargeObjectStorage,
            ["BlobManager"] = SymbolCategory.LargeObjectStorage,
            ["LockBytesSS"] = SymbolCategory.LargeObjectStorage,
            ["OpenLockBytes"] = SymbolCategory.LargeObjectStorage,
            ["GetDataAsILockBytes"] = SymbolCategory.LargeObjectStorage,
            ["InitStatsBlobHeaderAttributes"] = SymbolCategory.LargeObjectStorage,

            ["CTds"] = SymbolCategory.Networking,
            ["Tds"] = SymbolCategory.Networking,
            ["SNI"] = SymbolCategory.Networking,
            ["Net"] = SymbolCategory.Networking,

            ["ExecuteCommandsInAutoTransaction"] =
                SymbolCategory.TransactionManagement,
            ["Xact"] = SymbolCategory.TransactionManagement,
            ["Xdes"] = SymbolCategory.TransactionManagement,
            ["FullXact"] = SymbolCategory.TransactionManagement,
            ["CMsqlXact"] = SymbolCategory.TransactionManagement,
            ["ReadOnlyXact"] = SymbolCategory.TransactionManagement,
            ["SqlAutoReadOnlyXact"] = SymbolCategory.TransactionManagement,
            ["SqlAutoSimpleXact"] = SymbolCategory.TransactionManagement,

            ["AllocScan"] = SymbolCategory.AllocationAccess,
            ["AllocationOrderPageScanner"] = SymbolCategory.AllocationAccess,

            ["CMED"] = SymbolCategory.Metadata,
            ["CSQLStrings"] = SymbolCategory.Metadata,
            ["IMetadataAccess"] = SymbolCategory.Metadata,
            ["ECatBits"] = SymbolCategory.Metadata,
            ["QOMetadataLoader"] = SymbolCategory.Metadata,
            ["CMIterExtProp"] = SymbolCategory.Metadata,
            ["FLookupExtProperty"] = SymbolCategory.Metadata,
            ["ObtainExternalStreamingMetadata"] = SymbolCategory.Metadata,

            ["COpt"] = SymbolCategory.Optimization,
            ["CRelOp"] = SymbolCategory.Optimization,
            ["CAlg"] = SymbolCategory.Optimization,
            ["CRangeTable"] = SymbolCategory.Optimization,
            ["CEnvCollection"] = SymbolCategory.Optimization,
            ["COptExpr"] = SymbolCategory.Optimization,
            ["Pqo"] = SymbolCategory.Optimization,
            ["CQOS"] = SymbolCategory.Optimization,
            ["CLogOp"] = SymbolCategory.Optimization,
            ["CQuery"] = SymbolCategory.Optimization,
            ["CXteBuilder"] = SymbolCategory.Optimization,

            ["CCompPlan"] = SymbolCategory.Compilation,
            ["CStmt"] = SymbolCategory.Compilation,
            ["IUPController"] = SymbolCategory.Compilation,
            ["CProcHdr"] = SymbolCategory.Compilation,

            ["CAlgTableMetadata"] = SymbolCategory.QueryBinding,

            ["BaseThreadInitThunk"] = SymbolCategory.System,
            ["RtlUserThreadStart"] = SymbolCategory.System,

            ["CRcsSecurityInfo"] = SymbolCategory.Security,

            ["CQDS"] = SymbolCategory.QueryStore,
            ["CDBQDS"] = SymbolCategory.QueryStore,
            ["CLAQFeedbackManager"] = SymbolCategory.QueryStore,
            ["CQDSHintsApplier"] = SymbolCategory.QueryStore,
            ["GetFeedback"] = SymbolCategory.QueryStore,
            ["StoreStatementPlan"] = SymbolCategory.QueryStore,
            ["GetOriginalQueryHash"] = SymbolCategory.QueryStore,
            ["GetPlanOrOptReplayScriptToForce"] = SymbolCategory.QueryStore,

            ["SQLServerLogMgr"] = SymbolCategory.Logging,
            ["RecoveryUnit"] = SymbolCategory.Logging,
            ["WaitLogWritten"] = SymbolCategory.Logging,
            ["LogFlush"] = SymbolCategory.Logging,
            ["HardenLog"] = SymbolCategory.Logging,
        };

    private static readonly Dictionary<string, SymbolCategory> MethodCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GetRow"] = SymbolCategory.RowAccess,
            ["GetRowForKeyValue"] = SymbolCategory.RowAccess,
            ["ReleaseRow"] = SymbolCategory.RowAccess,
            ["OpenSystemTableRowset"] = SymbolCategory.RowAccess,
            ["ReleaseSystemTableRowset"] = SymbolCategory.RowAccess,

            ["DiskRead"] = SymbolCategory.IoInfrastructure,
            ["write_data"] = SymbolCategory.IoInfrastructure,
            ["flush_buffer"] = SymbolCategory.IoInfrastructure,
            ["JoinedIoCompletion"] = SymbolCategory.IoInfrastructure,

            ["AcquireLock"] = SymbolCategory.Locking,
            ["ReleaseLock"] = SymbolCategory.Locking,
            ["GetDataLock"] = SymbolCategory.Locking,
            ["GetHoBtLock"] = SymbolCategory.Locking,
            ["lck_"] = SymbolCategory.Locking,
            ["LockAndCheckState"] = SymbolCategory.Locking,
            ["CSQLLock"] = SymbolCategory.Locking,

            ["LogFlush"] = SymbolCategory.Logging,
            ["WaitLogWritten"] = SymbolCategory.Logging,
            ["HardenLog"] = SymbolCategory.Logging,
            ["WaitOnWriteAsyncToFinish"] = SymbolCategory.Logging,

            ["PqoBuild"] = SymbolCategory.Optimization,
            ["PrepareQuery"] = SymbolCategory.Optimization,

            ["BindTree"] = SymbolCategory.QueryBinding,

            ["FNormalize"] = SymbolCategory.Compilation,

            ["Publish"] = SymbolCategory.XEventInfrastructure,
            ["PublishWaitStatsXEvents"] = SymbolCategory.XEventInfrastructure,

            ["process_request"] = SymbolCategory.QueryExecution,
            ["process_commands"] = SymbolCategory.QueryExecution,
            ["process_messages"] = SymbolCategory.QueryExecution,
            ["ExecuteCommandsInAutoTransaction"] = SymbolCategory.QueryExecution,

            ["BaseThreadInitThunk"] = SymbolCategory.System,
            ["RtlUserThreadStart"] = SymbolCategory.System,

            ["OpenLockBytes"] = SymbolCategory.LargeObjectStorage,
            ["GetDataAsILockBytes"] = SymbolCategory.LargeObjectStorage,
            ["InitStatsBlobHeaderAttributes"] = SymbolCategory.LargeObjectStorage,

            ["AcquireGenericQdsDbAndProcess"] = SymbolCategory.QueryStore,
            ["FGetStatementContextIdAndEpoch"] = SymbolCategory.QueryStore,
            ["FindContextId"] = SymbolCategory.QueryStore,
            ["LlGetContextId"] = SymbolCategory.QueryStore,
            ["GetHintsToApply"] = SymbolCategory.QueryStore,
            ["GetOriginalQueryHash"] = SymbolCategory.QueryStore,
            ["GetPlanOrOptReplayScriptToForce"] = SymbolCategory.QueryStore,
            ["GetFeedback"] = SymbolCategory.QueryStore,
            ["StoreStatementPlan"] = SymbolCategory.QueryStore,
            ["FIsQDSStoredLocally"] = SymbolCategory.QueryStore,

            ["FLookupExtProperty"] = SymbolCategory.Metadata,
            ["ObtainExternalStreamingMetadata"] = SymbolCategory.Metadata,

            ["EvGetEventNotifications"] = SymbolCategory.Tracing,
            ["EvGetEventNotificationsToFire"] = SymbolCategory.Tracing,
            ["EvGetEvents"] = SymbolCategory.Tracing,
            ["EvGetTriggers"] = SymbolCategory.Tracing,
            ["EvGetTriggersToFire"] = SymbolCategory.Tracing,
            ["PostEventSessionEvent"] = SymbolCategory.Tracing,

            ["XEvent"] = SymbolCategory.XEventInfrastructure,
        };


    public static SymbolCategory GetCategory(string? className, string? methodName)
    {
        if (!string.IsNullOrWhiteSpace(className))
        {
            foreach (var rule in ClassCategories)
            {
                if (className.StartsWith(rule.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }
            }

            if (className.Contains("QDS", StringComparison.OrdinalIgnoreCase))
            {
                return SymbolCategory.QueryStore;
            }
        }

        if (!string.IsNullOrWhiteSpace(methodName))
        {
            foreach (var rule in MethodCategories)
            {
                if (methodName.StartsWith(rule.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }
            }
        }

        return SymbolCategory.Unknown;
    }
}