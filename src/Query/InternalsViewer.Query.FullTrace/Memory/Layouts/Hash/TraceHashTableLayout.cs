namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public static class TraceHashTableLayout
{
    public const string GroupClass = "CHashWorkfilePartitionGroup";

    public const int IteratorOffset = 0x10;

    public const int ActivePartitionOffset = 0xA0;

    public const int PartitionListOffset = 0xF0;

    public const int GroupOffset = 0x08;

    public const int RowCountOffset = 0x20;

    public const int DirectoryOffset = 0x48;

    public const int DirectoryCountOffset = 0x50;

    public const int BucketMaskOffset = 0x54;

    public const int PartitionLinkOffset = 0x98;

    public const int PartitionMaskOffset = 0x188;

    public const int PartitionShiftOffset = 0x18C;

    public const int BucketSize = 0x80;

    public const int BucketsPerPage = 64;

    public const int KeyOffset = 0x20;

    public const int RowOffset = 0x50;

    private const int PointerSize = sizeof(ulong);

    public static IReadOnlyList<MemoryField> GroupFields { get; } =
    [
        new("Iterator", IteratorOffset, PointerSize),
        new("Active Partition", ActivePartitionOffset, PointerSize),
        new("Last Partition", PartitionListOffset, PointerSize),
        new("First Partition", PartitionListOffset + PointerSize, PointerSize)
    ];

    public static IReadOnlyList<MemoryField> PartitionFields { get; } =
    [
        new("Group", GroupOffset, PointerSize),
        new("Row Count", RowCountOffset, sizeof(int)),
        new("Directory", DirectoryOffset, PointerSize),
        new("Directory Count", DirectoryCountOffset, sizeof(int)),
        new("Bucket Mask", BucketMaskOffset, sizeof(int), IsHex: true),
        new("Previous Partition", PartitionLinkOffset, PointerSize),
        new("Next Partition", PartitionLinkOffset + PointerSize, PointerSize)
    ];

    public static IReadOnlyList<MemoryField> BucketFields { get; } =
    [
        new("Row Count", 0, sizeof(byte)),
        new("Key", KeyOffset, sizeof(int)),
        new("Row", RowOffset, PointerSize)
    ];
}
