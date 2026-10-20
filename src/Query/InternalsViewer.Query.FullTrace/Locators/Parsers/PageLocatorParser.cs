using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class PageLocatorParser() : ValueLocatorParser(LocatorValueType.Page)
{
    private const int AllocationPageIdOffset = 0x8;

    private const int BufferPageIdOffset = 0x70;

    public static readonly int[] PageHandlePath = [0, 0x18];

    public static readonly int[] PageRefPath = [0];

    private static readonly string[] PageRefNames =
    [
        "PageRef", "BTreeHPage", "IndexPageRef", "HeapPageRef", "TextPageRef", "IAMPageRef", "GAMPageRef", "SGAMPageRef",
        "PFSPageRef", "PSPageRef"
    ];

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } =
    [
        new("PageId"),
        new("AllocationPageId", AllocationPageIdOffset),
        new("HPageHandle", BufferPageIdOffset, PageHandlePath),
        .. PageRefNames.Select(n => new LocatorTypeName(n, BufferPageIdOffset, PageRefPath))
    ];
}
