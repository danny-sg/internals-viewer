using InternalsViewer.Query.CallStack.TimeTravel.CallLog;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record MemoryFunction(ulong Address,
                                    MemoryOperation Operation,
                                    int SizeSlot,
                                    int PointerSlot,
                                    ulong Unit,
                                    int CountSlot = -1)
{
    private const ulong PageSize = 8192;

    private const ulong LargestAllocation = 1ul << 36;

    private const ulong MemCommit = 0x1000;

    private const ulong MemDecommit = 0x4000;

    private const ulong MemRelease = 0x8000;

    private const string MemoryObjectFactory = "MemoryObjectFactory";

    private static readonly HashSet<string> MemoryObjects = new(StringComparer.Ordinal)
    {
        "CMemObj",
        "IMemObj",
        "CMemProc",
        "CMemFixed",
        "CMemLargePageObj",
        "PageHeapMemObj"
    };

    public bool IsPage { get; init; }

    public bool CarriesClerk { get; init; }

    public bool IsHeap { get; init; }

    public bool IsObjectCall { get; init; }

    public bool IsVirtual { get; init; }

    public int FlagSlot { get; init; } = -1;

    public ulong FlagMask { get; init; }

    public bool Allocates => Operation is MemoryOperation.Allocate or MemoryOperation.Reallocate;

    public static bool IsMemoryObjectClass(string? className)
        => className is not null && (className == MemoryObjectFactory || IsMemoryObject(className));

    public bool Applies(TimeTravelArgumentCall call) => FlagSlot < 0 || (call.Slot(FlagSlot) & FlagMask) != 0;

    public ulong ObjectOf(TimeTravelArgumentCall call) => Operation == MemoryOperation.Create ? call.ReturnedValue : call.Slot(0);

    public ulong PointerOf(TimeTravelArgumentCall call) => call.Slot(PointerSlot);

    public ulong BytesOf(TimeTravelArgumentCall call)
    {
        if (SizeSlot < 0)
        {
            return Allocates ? Unit : 0;
        }

        var size = call.Slot(SizeSlot);

        var count = CountSlot < 0 ? 1 : call.Slot(CountSlot);

        if (size > LargestAllocation || count > LargestAllocation || Math.BigMul(size, count, out var bytes) != 0)
        {
            return 0;
        }

        return bytes <= LargestAllocation / Unit ? bytes * Unit : 0;
    }

    internal static MemoryFunction? Classify(ulong address, string symbol)
    {
        var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol);

        var className = separator >= 0 ? symbol[..separator] : null;

        var methodName = separator >= 0 ? symbol[(separator + 2)..] : symbol;

        return (className, methodName) switch
        {
            (null, "operator new" or "operator new[]")
                => new MemoryFunction(address, MemoryOperation.Allocate, 0, -1, 1),
            (null, "operator delete" or "operator delete[]")
                => new MemoryFunction(address, MemoryOperation.Free, -1, 0, 1),
            ("MemoryClerkInternal", "AllocatePages" or "AllocatePagesWithFailureMode" or "AllocateReservedPages")
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, PageSize) { IsPage = true, CarriesClerk = true },
            ("MemoryClerkInternal", "FreePages" or "FreeReservedPages")
                => new MemoryFunction(address, MemoryOperation.Free, 2, 1, PageSize) { IsPage = true, CarriesClerk = true },
            ("CQryMemManager", "AllocatePages")
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, PageSize) { IsPage = true },
            ("CQryMemManager", "FreePages")
                => new MemoryFunction(address, MemoryOperation.Free, 2, 1, PageSize) { IsPage = true },
            (MemoryObjectFactory, "CreateMemObject")
                => new MemoryFunction(address, MemoryOperation.Create, -1, -1, 1) { IsObjectCall = true },
            ("CHashWorkfilePartitionInstance", "PvAllocateHashBucketPage")
                => new MemoryFunction(address, MemoryOperation.Allocate, -1, -1, PageSize),
            ("CHashWorkfilePartitionInstance", "FreeHashBucketPage")
                => new MemoryFunction(address, MemoryOperation.Free, -1, 1, 1),
            ({ } owner, "Alloc") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
            ({ } owner, "Realloc") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Reallocate, 2, 1, 1) { IsObjectCall = true },
            ({ } owner, "Free") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Free, -1, 1, 1) { IsObjectCall = true },
            _ => null
        };
    }

    internal static MemoryFunction? ClassifyExport(ulong address, string export)
        => ClassifyVirtualExport(address, export)
           ?? (ClassifyHeapExport(address, export) is { } function ? function with { IsHeap = true } : null);

    private static MemoryFunction? ClassifyVirtualExport(ulong address, string export) => export switch
    {
        "VirtualAlloc"
            => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, 1)
            {
                IsPage = true,
                IsVirtual = true,
                FlagSlot = 2,
                FlagMask = MemCommit
            },
        "VirtualFree"
            => new MemoryFunction(address, MemoryOperation.Free, -1, 0, 1)
            {
                IsPage = true,
                IsVirtual = true,
                FlagSlot = 2,
                FlagMask = MemDecommit | MemRelease
            },
        _ => null
    };

    private static MemoryFunction? ClassifyHeapExport(ulong address, string export) => export switch
    {
        "RtlAllocateHeap" or "HeapAlloc"
            => new MemoryFunction(address, MemoryOperation.Allocate, 2, -1, 1),
        "RtlReAllocateHeap" or "HeapReAlloc"
            => new MemoryFunction(address, MemoryOperation.Reallocate, 3, 2, 1),
        "RtlFreeHeap" or "HeapFree"
            => new MemoryFunction(address, MemoryOperation.Free, -1, 2, 1),
        "LocalAlloc" or "GlobalAlloc"
            => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, 1),
        "LocalReAlloc" or "GlobalReAlloc"
            => new MemoryFunction(address, MemoryOperation.Reallocate, 1, 0, 1),
        "LocalFree" or "GlobalFree"
            => new MemoryFunction(address, MemoryOperation.Free, -1, 0, 1),
        "malloc" or "_aligned_malloc"
            => new MemoryFunction(address, MemoryOperation.Allocate, 0, -1, 1),
        "calloc"
            => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, 1, CountSlot: 0),
        "realloc" or "_aligned_realloc"
            => new MemoryFunction(address, MemoryOperation.Reallocate, 1, 0, 1),
        "_recalloc"
            => new MemoryFunction(address, MemoryOperation.Reallocate, 2, 0, 1, CountSlot: 1),
        "free" or "_aligned_free"
            => new MemoryFunction(address, MemoryOperation.Free, -1, 0, 1),
        _ => null
    };

    private static bool IsMemoryObject(string className)
        => className.StartsWith("CMemThread<", StringComparison.Ordinal) || MemoryObjects.Contains(className);
}
