namespace InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

public enum ObjectStage : byte
{
    [Category("Parse And Bind", ForegroundColor = "#2874A6")]
    ParseAndBind,

    [Category("Optimization", ForegroundColor = "#138D75")]
    Optimization,

    [Category("Compilation", ForegroundColor = "#7D3C98")]
    Compilation,

    [Category("Execution", ForegroundColor = "#239B56")]
    Execution,

    [Category("Values", ForegroundColor = "#B9770E")]
    Values,

    [Category("Value References", ForegroundColor = "#7D8C21")]
    ValueReferences,

    [Category("Storage", ForegroundColor = "#0600A0")]
    Storage,

    [Category("Metadata", ForegroundColor = "#AD1457")]
    Metadata,

    [Category("Transactions", ForegroundColor = "#922B21")]
    Transactions,

    [Category("Memory", ForegroundColor = "#5D6D7E")]
    Memory,

    [Category("Containers", ForegroundColor = "#808B96")]
    Containers,

    [Category("Query Store", ForegroundColor = "#6C5CE7")]
    QueryStore,

    [Category("Security", ForegroundColor = "#C0392B")]
    Security,

    [Category("TDS", ForegroundColor = "#E67E22")]
    Tds
}
