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
        ForegroundColor = "#BFBFBF",
        IsInfrastructure = true)]
    XEventInfrastructure,

    [Category(
        "Tracing",
        Description = "Tracing and diagnostic event collection",
        ForegroundColor = "#A6A6A6",
        IsInfrastructure = true)]
    Tracing,

    [Category(
        "SQL OS Scheduling",
        Description = "Schedulers, tasks, workers and the points where a task yields or sleeps",
        ForegroundColor = "#8E7CC3",
        IsInfrastructure = true)]
    Scheduling,

    [Category(
        "SQL OS Synchronisation",
        Description = "Mutexes, semaphores, events, spinlocks and the waits on them",
        ForegroundColor = "#4A235A",
        IsInfrastructure = true)]
    Synchronisation,

    [Category(
        "I/O Infrastructure",
        Description = "Asynchronous I/O processing and completion",
        ForegroundColor = "#1F3864",
        IsInfrastructure = true)]
    IoInfrastructure,

    [Category(
        "Query Execution",
        Description = "Statement and execution context processing and the execution tree",
        ForegroundColor = "#58D68D")]
    QueryExecution,

    [Category(
        "Statement Execution",
        Description = "Batch and statement execution",
        ForegroundColor = "#1E8449")]
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
        ForegroundColor = "#26A69A")]
    Rowset,

    [Category(
        "Expression Evaluation",
        Description = "Expression and scalar value evaluation",
        ForegroundColor = "#E36C09")]
    ExpressionEvaluation,

    [Category(
        "B-Tree Navigation",
        Description = "B-tree navigation from the root to the page holding a key",
        ForegroundColor = "#1E96B4")]
    BTreeNavigation,

    [Category(
        "Page Access",
        Description = "Page fixing and page retrieval",
        ForegroundColor = "#2F5597")]
    PageAccess,

    [Category(
        "Buffer Pool",
        Description = "Buffer cache management, buffer descriptors and dirty pages",
        ForegroundColor = "#76A0E2")]
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
        ForegroundColor = "#A05EB5",
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
        ForegroundColor = "#F1C232")]
    Networking,

    [Category(
        "Allocations",
        Description = "Allocation-order scanning and extent/page traversal",
        ForegroundColor = "#5B9BD5")]
    Allocations,

    [Category(
        "Transaction Management",
        Description = "Transaction lifecycle, commit, rollback and transaction state management",
        ForegroundColor = "#E06666")]
    TransactionManagement,

    [Category(
        "Compilation",
        Description = "Query compilation and plan construction",
        ForegroundColor = "#D9668F",
        BackgroundColor = "#D9668F")]
    Compilation,

    [Category(
        "Optimization",
        Description = "Query optimization and relational operator transformations",
        ForegroundColor = "#F08FB0",
        BackgroundColor = "#F08FB0")]
    Optimization,

    [Category(
        "Metadata",
        Description = "Metadata, catalog and object lookup infrastructure",
        ForegroundColor = "#A8436A",
        BackgroundColor = "#A8436A")]
    Metadata,

    [Category(
        "Query Binding",
        Description = "Object, schema and column binding",
        ForegroundColor = "#F6B8CE",
        BackgroundColor = "#F6B8CE")]
    QueryBinding,

    [Category(
        "Query Store",
        Description = "Query Store persistence, feedback, plan forcing and hint management",
        ForegroundColor = "#FAD4E1",
        BackgroundColor = "#FAD4E1")]
    QueryStore,

    [Category(
        "Logging",
        Description = "Transaction log, recovery and log flush processing",
        ForegroundColor = "#B0A89A",
        BackgroundColor = "#B0A89A")]
    Logging,

    [Category(
        "Security",
        Description = "Permission, security and access control evaluation",
        ForegroundColor = "#BF9000",
        BackgroundColor = "#BF9000")]
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
        ForegroundColor = "#44546A")]
    Storage,

    [Category(
        "Columnstore",
        Description = "Column segment, dictionary and row group access in the storage engine",
        ForegroundColor = "#3A6EA5")]
    ColumnStore,

    [Category(
        "XML",
        Description = "XML data type conversion, serialisation and XML index access",
        ForegroundColor = "#F8A65A")]
    Xml
}