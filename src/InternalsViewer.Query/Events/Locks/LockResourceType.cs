using System.ComponentModel;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Locks;

public enum LockResourceType
{
    [EventItemName("Unknown lock resource")]
    UnknownLockResource = 0,

    [EventItemName("Null resource")]
    NullResource = 1,

    [EventItemName("Database")]
    Database = 2,

    [EventItemName("File")]
    File = 3,

    [EventItemName("Unused")]
    Unused1 = 4,

    [EventItemName("Object")]
    Object = 5,

    [EventItemName("Page")]
    Page = 6,

    [EventItemName("Key")]
    Key = 7,

    [EventItemName("Extent")]
    Extent = 8,

    [EventItemName("Row identifier")]
    Rid = 9,

    [EventItemName("Application")]
    Application = 10,

    [EventItemName("Metadata")]
    Metadata = 11,

    [EventItemName("Heap or B-tree")]
    Hobt = 12,

    [EventItemName("Allocation unit")]
    AllocationUnit = 13,

    [EventItemName("OIB")]
    Oib = 14,

    [EventItemName("Rowgroup")]
    Rowgroup = 15,

    [EventItemName("Transaction")]
    Xact = 16,

    [EventItemName("Last resource sentinel")]
    LastResource = 17
}
