namespace InternalsViewer.Query.CallStack.Categories;

public enum ModuleCategory : byte
{
    [Category(
        "Unknown",
        Description = "Not yet classified",
        ForegroundColor = "#808080")]
    Unknown = 0,

    [Category(
        "Storage Engine",
        Description = "Pages, indexes, allocation maps, buffers and storage structures",
        ForegroundColor = "#4472C4")]
    StorageEngine,

    [Category(
        "Query Processor",
        Description = "Query compilation and execution",
        ForegroundColor = "#00B050")]
    QueryProcessor,

    [Category(
        "SQL OS",
        Description = "Schedulers, workers, memory management and I/O infrastructure",
        ForegroundColor = "#7030A0")]
    SqlOs,

    [Category(
        "SQL Server Host",
        Description = "Top-level SQL Server executable and hosting infrastructure",
        ForegroundColor = "#5B9BD5")]
    SqlServerHost,

    [Category(
        "System",
        Description = "Operating system and runtime thread infrastructure",
        ForegroundColor = "#7F7F7F",
        IsInfrastructure = true)]
    System,

    [Category(
    "Expression Services",
    Description = "Expression evaluation and scalar computation infrastructure",
    ForegroundColor = "#8E44AD")]
    ExpressionServices,

    [Category(
        "Query Store",
        Description = "Query Store persistence, plan forcing, hints and feedback",
        ForegroundColor = "#6C5CE7")]
    QueryStore,
}