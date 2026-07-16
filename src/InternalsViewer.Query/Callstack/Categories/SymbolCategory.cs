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
        Description = "Schedulers, tasks and execution scheduling",
        ForegroundColor = "#7030A0",
        IsInfrastructure = true)]
    Scheduling,

    [Category(
        "Worker Management",
        Description = "Workers and thread execution infrastructure",
        ForegroundColor = "#8064A2",
        IsInfrastructure = true)]
    WorkerManagement,

    [Category(
        "I/O Infrastructure",
        Description = "Asynchronous I/O processing and completion",
        ForegroundColor = "#974806",
        IsInfrastructure = true)]
    IoInfrastructure,

    [Category(
        "Query Execution",
        Description = "Statement and execution context processing",
        ForegroundColor = "#5B9BD5")]
    QueryExecution,

    [Category(
        "Statement Execution",
        Description = "Batch and statement execution",
        ForegroundColor = "#4F81BD")]
    StatementExecution,

    [Category(
        "Query Operator",
        Description = "Query plan operators such as compute scalar, joins and sorts",
        ForegroundColor = "#00B050")]
    QueryOperator,

    [Category(
        "Physical Operator",
        Description = "Query plan physical operators such as scans and seeks",
        ForegroundColor = "#01873e")]
    PhysicalOperator,

    [Category(
        "Row Access",
        Description = "Rowset and row retrieval infrastructure",
        ForegroundColor = "#70AD47")]
    RowAccess,

    [Category(
        "Expression Evaluation",
        Description = "Expression and scalar value evaluation",
        ForegroundColor = "#9966CC")]
    ExpressionEvaluation,

    [Category(
        "Execution Tree",
        Description = "Context execution tree",
        ForegroundColor = "#9966CC")]
    ExecutionTree,

    [Category(
        "Dataset",
        Description = "Dataset and session abstractions",
        ForegroundColor = "#92D050")]
    Dataset,

    [Category(
        "Index Access",
        Description = "B-Tree navigation and index access",
        ForegroundColor = "#4472C4")]
    IndexAccess,

    [Category(
        "Page Access",
        Description = "Page fixing and page retrieval",
        ForegroundColor = "#2F5597")]
    PageAccess,

    [Category(
        "Buffer Manager",
        Description = "Buffer access and page management",
        ForegroundColor = "#1F497D")]
    BufferManager,

    [Category(
        "Buffer Pool",
        Description = "Buffer cache management",
        ForegroundColor = "#17375E")]
    BufferPool,

    [Category(
        "File Control Block",
        Description = "Storage engine file and object access structures",
        ForegroundColor = "#76923C")]
    FileControlBlock,

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
        Description = "Storage/Recovery context",
        ForegroundColor = "#76923C")]
    Storage,

    [Category(
        "XML",
        Description = "XML data type conversion, serialisation and XML index access",
        ForegroundColor = "#8E44AD")]
    Xml
}

public static class CategoryEnumExtensions
{
    public static CategoryAttribute? GetCategoryMetadata(this Enum value)
    {
        var member = value.GetType()
                          .GetMember(value.ToString())
                          .FirstOrDefault();

        return member?.GetCustomAttributes(typeof(CategoryAttribute), false)
                      .Cast<CategoryAttribute>()
                      .FirstOrDefault();
    }
}