namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public static class TraceHashTableLayout
{
    public const string GroupClass = "CHashWorkfilePartitionGroup";

    public const string PartitionClass = "CHashWorkfilePartitionInstance";

    public const string WorktableGroupClass = "CHashWorktablePartitionGroup";

    public const string ValueClass = "CValHash";

    public const int IteratorGroupsOffset = 0x140;

    public const int IteratorGroupCount = 6;

    public const int GroupsPerSide = 3;

    public const int CurrentGroupOffset = 0x210;

    public const int IteratorOffset = 0x10;

    public const int ColumnsOffset = 0x18;

    public const int ScanColumnsOffset = 0x20;

    public const int ValueAccessorOffset = 0x38;

    public const int ValueRecordOffset = 0x10;

    public const int ValueSlotOffset = 0x2C;

    public const int SwitchPrimaryOffset = 0x10;

    public const int SwitchInputOffset = 0x18;

    public const int ActivePartitionOffset = 0xA0;

    public const int ScanPartitionOffset = 0xA8;

    public const int PartitionListOffset = 0xF0;

    public const int GroupOffset = 0x08;

    public const int RowCountOffset = 0x20;

    public const int DirectoryOffset = 0x48;

    public const int DirectoryCountOffset = 0x50;

    public const int BucketMaskOffset = 0x54;

    public const int PartitionLinkOffset = 0x98;

    public const int WorkfileOffset = 0x38;

    public const int BitmapOffset = 0xC8;

    public const int PartitionMaskOffset = 0x188;

    public const int PartitionShiftOffset = 0x18C;

    public const int BucketSize = 0x80;

    public const int BucketsPerPage = 64;

    public const int HeadOffset = 0x10;

    public const int NextOffset = 0x18;

    public const int KeyOffset = 0x20;

    public const int RowOffset = 0x50;

    public const int EntriesPerItem = 6;

    private const int PointerSize = sizeof(ulong);

    private const int EntrySize = sizeof(ulong);

    public static IReadOnlyList<MemoryField> IteratorFields { get; } =
    [
        new("Build Partition Group", IteratorGroupsOffset, PointerSize),
        new("Build Worktable Group", IteratorGroupsOffset + PointerSize, PointerSize),
        new("Build Rebuild Group", IteratorGroupsOffset + 2 * PointerSize, PointerSize),
        new("Probe Partition Group", IteratorGroupsOffset + 3 * PointerSize, PointerSize),
        new("Probe Worktable Group", IteratorGroupsOffset + 4 * PointerSize, PointerSize),
        new("Probe Rebuild Group", IteratorGroupsOffset + 5 * PointerSize, PointerSize),
        new("Partition Mask", PartitionMaskOffset, sizeof(uint), IsHex: true),
        new("Partition Shift", PartitionShiftOffset, sizeof(int)),
        new("Workfile Resource Manager", 0x1B0, PointerSize),
        new("Workfile Manager", 0x1B8, PointerSize),
        new("Current Partition Group", CurrentGroupOffset, PointerSize)
    ];

    public static IReadOnlyList<MemoryField> GroupFields { get; } =
    [
        new("Virtual Table", 0x00, PointerSize),
        new("Compiled Group", 0x08, PointerSize),
        new("Iterator", IteratorOffset, PointerSize),
        new("Columns", ColumnsOffset, PointerSize),
        new("Scan Columns", ScanColumnsOffset, PointerSize),
        new("Insert Expression", 0x28, PointerSize),
        new("Expression", 0x30, PointerSize),
        new("Update Expression", 0x38, PointerSize),
        new("Expression", 0x40, PointerSize),
        new("Expression", 0x48, PointerSize),
        new("Compare Expression", 0x50, PointerSize),
        new("Active Insert Expression", 0x70, PointerSize),
        new("Active Update Expression", 0x78, PointerSize),
        new("Column Info", 0x80, PointerSize),
        new("Workfile Group", 0x88, PointerSize),
        new("Workfile Group", 0x90, PointerSize),
        new("Column Flags", 0x98, PointerSize),
        new("Active Partition", ActivePartitionOffset, PointerSize),
        new("Scan Partition", ScanPartitionOffset, PointerSize),
        new("Last Partition", PartitionListOffset, PointerSize),
        new("First Partition", PartitionListOffset + PointerSize, PointerSize)
    ];

    public static IReadOnlyList<MemoryField> PartitionFields { get; } =
    [
        new("Virtual Table", 0x00, PointerSize),
        new("Group", GroupOffset, PointerSize),
        new("Flag", 0x10, sizeof(byte)),
        new("Workfile Has Rows", 0x14, sizeof(byte)),
        new("State", 0x18, sizeof(int), IsHex: true),
        new("Row Count", RowCountOffset, sizeof(int)),
        new("Instance Number", 0x28, sizeof(int)),
        new("Workfile Rowset", 0x30, PointerSize),
        new("Workfile Rowset", WorkfileOffset, PointerSize),
        new("Workfile Rowset", 0x40, PointerSize),
        new("Directory", DirectoryOffset, PointerSize),
        new("Directory Count", DirectoryCountOffset, sizeof(int)),
        new("Bucket Mask", BucketMaskOffset, sizeof(int), IsHex: true),
        new("Initialised", 0x58, sizeof(byte)),
        new("Current Item", 0x60, PointerSize),
        new("Entry Index", 0x68, sizeof(int)),
        new("Last Lookup Key", 0x70, sizeof(int)),
        new("Overflow Page", 0x78, PointerSize),
        new("Overflow Items Left", 0x80, sizeof(int)),
        new("Overflow Total", 0x88, sizeof(int)),
        new("Page Count", 0x90, sizeof(int)),
        new("Previous Partition", PartitionLinkOffset, PointerSize),
        new("Next Partition", PartitionLinkOffset + PointerSize, PointerSize),
        new("Bitmap State", 0xA8, sizeof(int)),
        new("Bitmap", 0xB0, PointerSize),
        new("Bitmap Setting", 0xB8, sizeof(int)),
        new("Bitmap Setting", 0xBC, sizeof(int)),
        new("Bitmap Mask", 0xC0, sizeof(int), IsHex: true),
        new("Bitmap Pages", BitmapOffset, PointerSize)
    ];

    public static IReadOnlyList<MemoryField> ItemFields { get; } =
    [
        new("Count", 0, sizeof(byte)),
        new("Head", HeadOffset, PointerSize),
        new("Next", NextOffset, PointerSize),
        new("Key", KeyOffset, sizeof(int)),
        new("Row", RowOffset, PointerSize)
    ];

    public static int GetKeyOffset(int entry) => KeyOffset + entry * EntrySize;

    public static int GetRowOffset(int entry) => RowOffset + entry * EntrySize;
}
