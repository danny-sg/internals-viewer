using System.ComponentModel;

namespace InternalsViewer.Replay.Locks;

public enum LockResourceType
{
    [Description("Unknown lock resource")]
    UnknownLockResource = 0,

    [Description("Null resource")]
    NullResource = 1,

    [Description("Database")]
    Database = 2,

    [Description("File")]
    File = 3,

    [Description("Unused")]
    Unused1 = 4,

    [Description("Object")]
    Object = 5,

    [Description("Page")]
    Page = 6,

    [Description("Key")]
    Key = 7,

    [Description("Extent")]
    Extent = 8,

    [Description("Row identifier")]
    Rid = 9,

    [Description("Application")]
    Application = 10,

    [Description("Metadata")]
    Metadata = 11,

    [Description("Heap or B-tree")]
    Hobt = 12,

    [Description("Allocation unit")]
    AllocationUnit = 13,

    [Description("OIB")]
    Oib = 14,

    [Description("Rowgroup")]
    Rowgroup = 15,

    [Description("Transaction")]
    Xact = 16,

    [Description("Last resource sentinel")]
    LastResource = 17
}
