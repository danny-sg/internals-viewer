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

    private static readonly HashSet<string> MemoryObjects = new(StringComparer.Ordinal)
    {
        "CMemObj",
        "IMemObj",
        "CMemProc",
        "CMemFixed",
        "CMemLargePageObj",
        "PageHeapMemObj"
    };

    public bool Allocates => Operation is MemoryOperation.Allocate or MemoryOperation.Reallocate;

    public ulong BytesOf(TimeTravelArgumentCall call)
    {
        if (SizeSlot < 0)
        {
            return Allocates ? Unit : 0;
        }

        if (SizeSlot >= call.IntegerSlots.Length)
        {
            return 0;
        }

        var size = call.IntegerSlots[SizeSlot];

        var count = CountSlot >= 0 && CountSlot < call.IntegerSlots.Length ? call.IntegerSlots[CountSlot] : 1;

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
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, PageSize),
            ("MemoryClerkInternal", "FreePages" or "FreeReservedPages")
                => new MemoryFunction(address, MemoryOperation.Free, 2, 1, PageSize),
            ("CQryMemManager", "AllocatePages")
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, PageSize),
            ("CQryMemManager", "FreePages")
                => new MemoryFunction(address, MemoryOperation.Free, 2, 1, PageSize),
            ("CHashWorkfilePartitionInstance", "PvAllocateHashBucketPage")
                => new MemoryFunction(address, MemoryOperation.Allocate, -1, -1, PageSize),
            ("CHashWorkfilePartitionInstance", "FreeHashBucketPage")
                => new MemoryFunction(address, MemoryOperation.Free, -1, 1, 1),
            ({ } owner, "Alloc") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Allocate, 1, -1, 1),
            ({ } owner, "Realloc") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Reallocate, 2, 1, 1),
            ({ } owner, "Free") when IsMemoryObject(owner)
                => new MemoryFunction(address, MemoryOperation.Free, -1, 1, 1),
            _ => null
        };
    }

    internal static MemoryFunction? ClassifyExport(ulong address, string export) => export switch
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
