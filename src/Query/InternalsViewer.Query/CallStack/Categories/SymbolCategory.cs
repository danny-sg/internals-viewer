namespace InternalsViewer.Query.CallStack.Categories;

public enum SymbolCategory : byte
{
    // Named (rather than blank) so an unclassified frame still shows a category — CallStackNode.Category falls back to
    // the module only when the symbol is UNRESOLVED, and an empty name is not null, so a blank here renders an empty tag
    // rather than falling through to anything.
    [Category(
        "Unknown",
        Description = "Not yet classified",
        ForegroundColor = "#808080")]
    Unknown = 0,

    [Category(
        "Extended Events",
        Description = "Extended Events publishing and action processing",
        ForegroundColor = "#C00000",
        IsInfrastructure = true)]
    XEventInfrastructure,

    [Category(
        "Tracing",
        Description = "Tracing and diagnostic event collection",
        ForegroundColor = "#E36C09",
        IsInfrastructure = true)]
    Tracing,

    [Category(
        "SQL OS Scheduling",
        Description = "Schedulers, tasks, workers and the points where a task yields or sleeps",
        ForegroundColor = "#7030A0",
        IsInfrastructure = true)]
    Scheduling,

    [Category(
        "SQL OS Synchronisation",
        Description = "Mutexes, semaphores, events, spinlocks and the waits on them",
        ForegroundColor = "#7030A0",
        IsInfrastructure = true)]
    Synchronisation,

    [Category(
        "I/O Infrastructure",
        Description = "Asynchronous I/O processing and completion",
        ForegroundColor = "#974806",
        IsInfrastructure = true)]
    IoInfrastructure,

    [Category(
        "Query Execution",
        Description = "Statement and execution context processing and the execution tree",
        ForegroundColor = "#5B9BD5")]
    QueryExecution,

    [Category(
        "Statement Execution",
        Description = "Batch and statement execution",
        ForegroundColor = "#4F81BD")]
    StatementExecution,

    [Category(
        "Query Operator",
        Description = "Query plan operators such as scans, seeks, joins, sorts and hash matches",
        ForegroundColor = "#00B050")]
    QueryOperator,

    [Category(
        "Batch Operator",
        Description = "Batch mode operators, their hash tables, aggregation and spills",
        ForegroundColor = "#01873e")]
    BatchOperator,

    [Category(
        "Rowset",
        Description = "The rowset interface operators call, the heap and index sessions behind it, and record decoding",
        ForegroundColor = "#70AD47")]
    Rowset,

    [Category(
        "Expression Evaluation",
        Description = "Expression and scalar value evaluation",
        ForegroundColor = "#9966CC")]
    ExpressionEvaluation,

    [Category(
        "B-Tree Navigation",
        Description = "B-tree navigation from the root to the page holding a key",
        ForegroundColor = "#4472C4")]
    BTreeNavigation,

    [Category(
        "Page Access",
        Description = "Page fixing and page retrieval",
        ForegroundColor = "#2F5597")]
    PageAccess,

    [Category(
        "Buffer Pool",
        Description = "Buffer cache management, buffer descriptors and dirty pages",
        ForegroundColor = "#0026E4")]
    BufferPool,

    [Category(
        "Locking",
        Description = "Lock acquisition and management",
        ForegroundColor = "#953735")]
    Locking,

    [Category(
        "Latching",
        Description = "In-memory synchronization primitives",
        ForegroundColor = "#C0504D")]
    Latching,

    [Category(
        "SQL OS",
        Description = "SQL OS infrastructure",
        ForegroundColor = "#7030A0",
        IsInfrastructure = true)]
    SqlOs,

    [Category(
        "Memory",
        Description = "Memory objects, allocators, clerks and query memory grants",
        ForegroundColor = "#B2BEC3",
        IsInfrastructure = true)]
    Memory,

    [Category(
        "LOB Storage",
        Description = "Large object (LOB) and off-row data access",
        ForegroundColor = "#4BACC6")]
    LargeObjectStorage,

    [Category(
        "Networking",
        Description = "Tabular Data Stream (TDS) protocol processing and client communication",
        ForegroundColor = "#F79646")]
    Networking,

    [Category(
        "Allocations",
        Description = "Allocation-order scanning and extent/page traversal",
        ForegroundColor = "#5B9BD5")]
    Allocations,

    [Category(
        "Transaction Management",
        Description = "Transaction lifecycle, commit, rollback and transaction state management",
        ForegroundColor = "#B45F06")]
    TransactionManagement,

    [Category(
        "Compilation",
        Description = "Query compilation and plan construction",
        ForegroundColor = "#16A085",
        BackgroundColor = "#16A085")]
    Compilation,

    [Category(
        "Optimization",
        Description = "Query optimization and relational operator transformations",
        ForegroundColor = "#1ABC9C",
        BackgroundColor = "#1ABC9C")]
    Optimization,

    [Category(
        "Metadata",
        Description = "Metadata, catalog and object lookup infrastructure",
        ForegroundColor = "#27AE60",
        BackgroundColor = "#27AE60")]
    Metadata,

    [Category(
        "Query Binding",
        Description = "Object, schema and column binding",
        ForegroundColor = "#2980B9",
        BackgroundColor = "#2980B9")]
    QueryBinding,

    [Category(
        "Query Store",
        Description = "Query Store persistence, feedback, plan forcing and hint management",
        ForegroundColor = "#6C5CE7",
        BackgroundColor = "#6C5CE7")]
    QueryStore,

    [Category(
        "Logging",
        Description = "Transaction log, recovery and log flush processing",
        ForegroundColor = "#E17055",
        BackgroundColor = "#E17055")]
    Logging,

    [Category(
        "Security",
        Description = "Permission, security and access control evaluation",
        ForegroundColor = "#D63031",
        BackgroundColor = "#D63031")]
    Security,

    [Category(
        "System",
        Description = "Windows thread and runtime infrastructure",
        ForegroundColor = "#636E72",
        BackgroundColor = "#636E72",
        IsInfrastructure = true)]
    System,

    [Category(
        "Storage",
        Description = "Storage and recovery context, HoBts, worktables and file control blocks",
        ForegroundColor = "#76923C")]
    Storage,

    [Category(
        "Columnstore",
        Description = "Column segment, dictionary and row group access in the storage engine",
        ForegroundColor = "#2E8B90")]
    ColumnStore,

    [Category(
        "XML",
        Description = "XML data type conversion, serialisation and XML index access",
        ForegroundColor = "#8E44AD")]
    Xml
}