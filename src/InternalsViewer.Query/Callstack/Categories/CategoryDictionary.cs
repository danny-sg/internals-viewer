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
            ["SOS_Task"] = SymbolCategory.SqlOs,
            ["SOS_Scheduler"] = SymbolCategory.SqlOs,
            ["Worker"] = SymbolCategory.SqlOs,
            ["ThreadScheduler"] = SymbolCategory.SqlOs,
            ["SchedulerManager"] = SymbolCategory.SqlOs,
            ["IOQueue"] = SymbolCategory.IoInfrastructure,
            ["CMsqlExecContext"] = SymbolCategory.QueryExecution,
            ["CSQLSource"] = SymbolCategory.QueryExecution,
            ["CXStmt"] = SymbolCategory.QueryExecution,
            ["CQScan"] = SymbolCategory.QueryOperator,
            ["BTree"] = SymbolCategory.IndexAccess,
            ["BUF"] = SymbolCategory.BufferManager,
            ["BPool"] = SymbolCategory.BufferPool,
            ["FCB"] = SymbolCategory.FileControlBlock,
            ["Rowset"] = SymbolCategory.RowAccess,
            ["DataSet"] = SymbolCategory.Dataset,
            ["FixPage"] = SymbolCategory.PageAccess,
            ["Latch"] = SymbolCategory.Latching,
            ["Lock"] = SymbolCategory.Locking,
            ["Page"] = SymbolCategory.PageAccess,
            ["WaitableBase"] = SymbolCategory.Scheduling,
            ["IndexPageManager"] = SymbolCategory.IndexAccess,
            ["IndexRowScanner"] = SymbolCategory.RowAccess,
            ["IndexDataSetSession"] = SymbolCategory.Dataset,
            ["QueryScan"] = SymbolCategory.QueryOperator,
            ["CQueryScan"] = SymbolCategory.QueryOperator,
            ["CStmtPrepQuery"] = SymbolCategory.StatementExecution,
            ["CExecStmtLoopVars"] = SymbolCategory.StatementExecution,
            ["CProfileInfo"] = SymbolCategory.XEventInfrastructure,

            ["CBatchTraceHelper"] = SymbolCategory.Tracing,
            ["CLanguageExecEnv"] = SymbolCategory.QueryExecution,
            ["SystemThreadDispatcher"] = SymbolCategory.WorkerManagement,
            ["CValFetchByKey"] = SymbolCategory.RowAccess,
            ["CEsExec"] = SymbolCategory.ExpressionEvaluation,

            ["Blob"] = SymbolCategory.LargeObjectStorage,
            ["BlobBase"] = SymbolCategory.LargeObjectStorage,
            ["BlobManager"] = SymbolCategory.LargeObjectStorage,
            ["LockBytesSS"] = SymbolCategory.LargeObjectStorage,

            ["CTds"] = SymbolCategory.Networking,
            ["Tds"] = SymbolCategory.Networking,
            ["SNI"] = SymbolCategory.Networking,
            ["Net"] = SymbolCategory.Networking,


            ["ExecuteCommandsInAutoTransaction"] =
                SymbolCategory.TransactionManagement,

            ["AllocScan"] = SymbolCategory.AllocationAccess,
            ["AllocationOrderPageScanner"] = SymbolCategory.AllocationAccess,
            ["HeapPageManager"] = SymbolCategory.PageAccess,
            ["HeapDataSetSession"] = SymbolCategory.Dataset,

            ["Xact"] = SymbolCategory.TransactionManagement,
            ["Xdes"] = SymbolCategory.TransactionManagement,
            ["FullXact"] = SymbolCategory.TransactionManagement,
            ["CMsqlXact"] = SymbolCategory.TransactionManagement,


            ["CMED"] = SymbolCategory.Metadata,

            ["COpt"] = SymbolCategory.Optimization,
            ["CRelOp"] = SymbolCategory.Optimization,
            ["CAlg"] = SymbolCategory.Optimization,
            ["CRangeTable"] = SymbolCategory.Optimization,
            ["CEnvCollection"] = SymbolCategory.Optimization,

            ["CCompPlan"] = SymbolCategory.Compilation,
            ["CStmt"] = SymbolCategory.Compilation,

            ["lck_"] = SymbolCategory.Locking,
            ["MDL"] = SymbolCategory.Locking,

            ["CSQLLock"] = SymbolCategory.Locking,
            ["CSQLStrings"] = SymbolCategory.Metadata,
            ["IUPController"] = SymbolCategory.Compilation,

            ["COptExpr"] = SymbolCategory.Optimization,
            ["CProcHdr"] = SymbolCategory.Compilation,

            ["CAlgTableMetadata"] = SymbolCategory.QueryBinding,


            ["BaseThreadInitThunk"] = SymbolCategory.System,
            ["RtlUserThreadStart"] = SymbolCategory.System,

            ["GetDataLock"] = SymbolCategory.Locking,
            ["GetHoBtLock"] = SymbolCategory.Locking,
            ["LockAndCheckState"] = SymbolCategory.Locking,
            ["AutoLockedHoBt"] = SymbolCategory.Locking,

            ["AcquireLock"] = SymbolCategory.Locking,
            ["SMD"] = SymbolCategory.Locking,


            ["IMetadataAccess"] = SymbolCategory.Metadata,
            ["ECatBits"] = SymbolCategory.Metadata,

            ["QOMetadataLoader"] = SymbolCategory.Metadata,

            ["CMIterExtProp"] = SymbolCategory.Metadata,
            ["FLookupExtProperty"] = SymbolCategory.Metadata,
            ["ObtainExternalStreamingMetadata"] = SymbolCategory.Metadata,


            ["ReadOnlyXact"] = SymbolCategory.TransactionManagement,
            ["SqlAutoReadOnlyXact"] = SymbolCategory.TransactionManagement,
            ["SqlAutoSimpleXact"] = SymbolCategory.TransactionManagement,


            ["CQDS"] = SymbolCategory.QueryStore,
            ["CDBQDS"] = SymbolCategory.QueryStore,

            ["CLAQFeedbackManager"] = SymbolCategory.QueryStore,
            ["CQDSHintsApplier"] = SymbolCategory.QueryStore,


            ["Pqo"] = SymbolCategory.Optimization,
            ["CQOS"] = SymbolCategory.Optimization,
            ["CLogOp"] = SymbolCategory.Optimization,

            ["CQuery"] = SymbolCategory.Optimization,
            ["CXteBuilder"] = SymbolCategory.Optimization,


            ["OpenRowset"] = SymbolCategory.RowAccess,
            ["OpenSystemTableRowset"] = SymbolCategory.RowAccess,

            ["FsInternalTableAccess"] = SymbolCategory.RowAccess,

            ["GetRowForKeyValue"] = SymbolCategory.RowAccess,


            ["OpenLockBytes"] = SymbolCategory.LargeObjectStorage,
            ["GetDataAsILockBytes"] = SymbolCategory.LargeObjectStorage,

            ["InitStatsBlobHeaderAttributes"] = SymbolCategory.LargeObjectStorage,

            ["CRcsSecurityInfo"] = SymbolCategory.Security,


            ["SQLServerLogMgr"] = SymbolCategory.Logging,
            ["RecoveryUnit"] = SymbolCategory.Logging,

            ["WaitLogWritten"] = SymbolCategory.Logging,
            ["LogFlush"] = SymbolCategory.Logging,
            ["HardenLog"] = SymbolCategory.Logging,


            ["DiskReadAsync"] = SymbolCategory.IoInfrastructure,
            ["JoinedIoCompletion"] = SymbolCategory.IoInfrastructure,

            ["WaitOnWriteAsyncToFinish"] = SymbolCategory.IoInfrastructure,
            ["write_data"] = SymbolCategory.IoInfrastructure,
            ["flush_buffer"] = SymbolCategory.IoInfrastructure,


            ["EvGet"] = SymbolCategory.Tracing,
            ["PostEventSessionEvent"] = SymbolCategory.Tracing,

            ["CMIterTriggers"] = SymbolCategory.Tracing,


            ["XE_"] = SymbolCategory.XEventInfrastructure,

            ["XeSosPkg"] = SymbolCategory.XEventInfrastructure,

            ["PublishWaitStatsXEvents"] =
                SymbolCategory.XEventInfrastructure,

            ["CExecuteStatement"] =
                SymbolCategory.QueryExecution,

            ["GetFeedback"] = SymbolCategory.QueryStore,
            ["StoreStatementPlan"] = SymbolCategory.QueryStore,
            ["GetOriginalQueryHash"] = SymbolCategory.QueryStore,
            ["GetPlanOrOptReplayScriptToForce"] = SymbolCategory.QueryStore,
        };

    private static readonly Dictionary<string, SymbolCategory> MethodCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {

            ["GetRow"] = SymbolCategory.RowAccess,
            ["GetRowForKeyValue"] = SymbolCategory.RowAccess,
            ["ReleaseRow"] = SymbolCategory.RowAccess,


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


            ["PqoBuild"] = SymbolCategory.Optimization,
            ["BindTree"] = SymbolCategory.QueryBinding,
            ["PrepareQuery"] = SymbolCategory.Optimization,
            ["FNormalize"] = SymbolCategory.Compilation,

            ["Publish"] = SymbolCategory.XEventInfrastructure,
            ["PublishWaitStatsXEvents"] = SymbolCategory.XEventInfrastructure,


            ["process_request"] = SymbolCategory.QueryExecution,
            ["process_commands"] = SymbolCategory.QueryExecution,
            ["process_messages"] = SymbolCategory.QueryExecution,


            ["BaseThreadInitThunk"] = SymbolCategory.System,
            ["RtlUserThreadStart"] = SymbolCategory.System,


            ["OpenSystemTableRowset"] = SymbolCategory.RowAccess,
            ["ReleaseSystemTableRowset"] = SymbolCategory.RowAccess,


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

            ["ExecuteCommandsInAutoTransaction"] = SymbolCategory.QueryExecution,


            ["FLookupExtProperty"] = SymbolCategory.Metadata,
            ["ObtainExternalStreamingMetadata"] = SymbolCategory.Metadata,

            ["WaitOnWriteAsyncToFinish"] = SymbolCategory.Logging,


            ["EvGetEventNotifications"] = SymbolCategory.Tracing,
            ["EvGetEventNotificationsToFire"] = SymbolCategory.Tracing,
            ["EvGetEvents"] = SymbolCategory.Tracing,
            ["EvGetTriggers"] = SymbolCategory.Tracing,
            ["EvGetTriggersToFire"] = SymbolCategory.Tracing,
            ["PostEventSessionEvent"] = SymbolCategory.Tracing,

            ["SOSHost_EventAuto"] = SymbolCategory.Scheduling,
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

            if (className.Contains("QDS"))
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