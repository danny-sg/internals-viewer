namespace InternalsViewer.Query.Callstack.Categories;

public enum SymbolCategory : byte
{
    [Category(
        "Unknown",
        Description = "Not yet classified",
        ForegroundColor = "#FFFFFF",
        BackgroundColor = "#FFFFFF")]
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
        Description = "Query plan operators such as scans, joins and sorts",
        ForegroundColor = "#00B050")]
    QueryOperator,

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
        "Lock Manager",
        Description = "Lock acquisition and management",
        ForegroundColor = "#953735")]
    LockManager,

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
        "Allocation Access",
        Description = "Allocation-order scanning and extent/page traversal",
        ForegroundColor = "#5B9BD5")]
    AllocationAccess,

}

public static class EnumExtensions
{
    public static CategoryAttribute? GetCategoryMetadata(
        this Enum value)
    {
        var member =
            value.GetType()
                .GetMember(value.ToString())
                .FirstOrDefault();

        return member?
            .GetCustomAttributes(
                typeof(CategoryAttribute),
                false)
            .Cast<CategoryAttribute>()
            .FirstOrDefault();
    }
}