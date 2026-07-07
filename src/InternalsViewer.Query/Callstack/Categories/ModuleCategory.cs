using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace InternalsViewer.Query.Callstack.Categories;


public enum ModuleCategory : byte
{
    [Description("Unknown")]
    Unknown = 0,

    [Description("Storage Engine")]
    StorageEngine,

    [Description("Query Processor")]
    QueryProcessor,

    [Description("SQL OS")]
    SqlOs,

    [Description("SQL Server Host")]
    SqlServerHost
}

public enum SymbolCategory : byte
{
    [Description("Unknown")]
    Unknown = 0,

    [Description("Query Operator")]
    QueryOperator,

    [Description("Query Execution")]
    QueryExecution,

    [Description("Row Access")]
    RowAccess,

    [Description("Dataset")]
    Dataset,

    [Description("Index Access")]
    IndexAccess,

    [Description("Buffer Manager")]
    BufferManager,

    [Description("Buffer Pool")]
    BufferPool,

    [Description("SQL OS")]
    SqlOs,

    [Description("I/O Infrastructure")]
    IoInfrastructure,

    [Description("Extended Events")]
    XEventInfrastructure
}