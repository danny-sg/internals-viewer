using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

internal sealed record MemoryFunction(ulong Address,
                                      AllocationOperation Operation,
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

    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> MemoryObjectLookup =
        MemoryObjects.GetAlternateLookup<ReadOnlySpan<char>>();

    public bool IsPage { get; init; }

    public bool CarriesClerk { get; init; }

    public bool IsHeap { get; init; }

    public bool IsObjectCall { get; init; }

    public bool IsVirtual { get; init; }

    public int FlagSlot { get; init; } = -1;

    public ulong FlagMask { get; init; }

    public bool Allocates => Operation is AllocationOperation.Allocate or AllocationOperation.Reallocate;

    public static bool IsMemoryObjectClass(string? className)
        => className is not null && (className == MemoryObjectFactory || IsMemoryObject(className));

    public bool Applies(TraceArgumentCall call) => FlagSlot < 0 || (call.Slot(FlagSlot) & FlagMask) != 0;

    public ulong ObjectOf(TraceArgumentCall call) => Operation == AllocationOperation.Create ? call.ReturnedValue : call.Slot(0);

    public ulong PointerOf(TraceArgumentCall call) => call.Slot(PointerSlot);

    public ulong BytesOf(TraceArgumentCall call)
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

    internal static MemoryFunction? Classify(ulong address, ReadOnlySpan<char> symbol)
    {
        var separator = SymbolsHelper.FindClassMethodSeparator(symbol);

        if (separator < 0)
        {
            return symbol switch
            {
                "operator new" or "operator new[]"
                    => new MemoryFunction(address, AllocationOperation.Allocate, 0, -1, 1),
                "operator delete" or "operator delete[]"
                    => new MemoryFunction(address, AllocationOperation.Free, -1, 0, 1),
                _ => null
            };
        }

        var className = symbol[..separator];

        var methodName = symbol[(separator + 2)..];

        return className switch
        {
            "MemoryClerkInternal" => methodName switch
            {
                "AllocatePages" or "AllocatePagesWithFailureMode" or "AllocateReservedPages"
                    => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, PageSize) { IsPage = true, CarriesClerk = true },
                "FreePages" or "FreeReservedPages"
                    => new MemoryFunction(address, AllocationOperation.Free, 2, 1, PageSize) { IsPage = true, CarriesClerk = true },
                _ => null
            },
            "CQryMemManager" => methodName switch
            {
                "AllocatePages" => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, PageSize) { IsPage = true },
                "FreePages" => new MemoryFunction(address, AllocationOperation.Free, 2, 1, PageSize) { IsPage = true },
                _ => null
            },
            MemoryObjectFactory => methodName is "CreateMemObject"
                ? new MemoryFunction(address, AllocationOperation.Create, -1, -1, 1) { IsObjectCall = true }
                : null,
            "CHashWorkfilePartitionInstance" => methodName switch
            {
                "PvAllocateHashBucketPage" => new MemoryFunction(address, AllocationOperation.Allocate, -1, -1, PageSize),
                "FreeHashBucketPage" => new MemoryFunction(address, AllocationOperation.Free, -1, 1, 1),
                _ => null
            },
            _ when IsMemoryObject(className) => methodName switch
            {
                "Alloc" => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
                "Realloc" => new MemoryFunction(address, AllocationOperation.Reallocate, 2, 1, 1) { IsObjectCall = true },
                "Free" => new MemoryFunction(address, AllocationOperation.Free, -1, 1, 1) { IsObjectCall = true },
                _ => null
            },
            _ => null
        };
    }

    internal static MemoryFunction? ClassifyExport(ulong address, string export)
        => ClassifyVirtualExport(address, export)
           ?? (ClassifyHeapExport(address, export) is { } function ? function with { IsHeap = true } : null);

    private static MemoryFunction? ClassifyVirtualExport(ulong address, string export) => export switch
    {
        "VirtualAlloc"
            => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, 1)
            {
                IsPage = true,
                IsVirtual = true,
                FlagSlot = 2,
                FlagMask = MemCommit
            },
        "VirtualFree"
            => new MemoryFunction(address, AllocationOperation.Free, -1, 0, 1)
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
            => new MemoryFunction(address, AllocationOperation.Allocate, 2, -1, 1),
        "RtlReAllocateHeap" or "HeapReAlloc"
            => new MemoryFunction(address, AllocationOperation.Reallocate, 3, 2, 1),
        "RtlFreeHeap" or "HeapFree"
            => new MemoryFunction(address, AllocationOperation.Free, -1, 2, 1),
        "LocalAlloc" or "GlobalAlloc"
            => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, 1),
        "LocalReAlloc" or "GlobalReAlloc"
            => new MemoryFunction(address, AllocationOperation.Reallocate, 1, 0, 1),
        "LocalFree" or "GlobalFree"
            => new MemoryFunction(address, AllocationOperation.Free, -1, 0, 1),
        "malloc" or "_aligned_malloc"
            => new MemoryFunction(address, AllocationOperation.Allocate, 0, -1, 1),
        "calloc"
            => new MemoryFunction(address, AllocationOperation.Allocate, 1, -1, 1, CountSlot: 0),
        "realloc" or "_aligned_realloc"
            => new MemoryFunction(address, AllocationOperation.Reallocate, 1, 0, 1),
        "_recalloc"
            => new MemoryFunction(address, AllocationOperation.Reallocate, 2, 0, 1, CountSlot: 1),
        "free" or "_aligned_free"
            => new MemoryFunction(address, AllocationOperation.Free, -1, 0, 1),
        _ => null
    };

    private static bool IsMemoryObject(ReadOnlySpan<char> className)
        => className.StartsWith("CMemThread<", StringComparison.Ordinal) || MemoryObjectLookup.Contains(className);
}
