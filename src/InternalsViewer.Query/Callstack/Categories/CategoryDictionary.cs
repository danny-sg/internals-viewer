using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Query.Callstack.Categories;

internal class SymbolCategoryDictionary
{
    private static readonly Dictionary<string, SymbolCategory> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GenericEvent"] = SymbolCategory.XEventInfrastructure,
            ["XeSqlPkg"] = SymbolCategory.XEventInfrastructure,
            ["XEvent"] = SymbolCategory.XEventInfrastructure,
            ["CTraceData"] = SymbolCategory.XEventInfrastructure,
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
            ["Lock"] = SymbolCategory.LockManager,
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

            ["AllocScan"] = SymbolCategory.AllocationAccess,
            ["AllocationOrderPageScanner"] = SymbolCategory.AllocationAccess,
            ["HeapPageManager"] = SymbolCategory.PageAccess,
            ["HeapDataSetSession"] = SymbolCategory.Dataset,
        };


    public static SymbolCategory GetCategory(string? symbolClass)
    {
        if (string.IsNullOrWhiteSpace(symbolClass))
        {
            return SymbolCategory.Unknown;
        }

        foreach (var entry in Categories)
        {
            if (symbolClass.StartsWith(
                    entry.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return SymbolCategory.Unknown;
    }
}