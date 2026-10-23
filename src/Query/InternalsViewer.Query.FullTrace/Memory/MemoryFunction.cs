using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Memory.Enums;

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

    private static readonly char[] PathSeparators = ['\\', '/'];

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

    private static readonly HashSet<string> AllocatorFunctions = new(StringComparer.Ordinal)
    {
        "OperatorNew",
        "malloc",
        "calloc",
        "_aligned_malloc"
    };

    private static readonly string[] AllocatorClassPrefixes = ["CMemPartitioned<", "TVarPageMgr<"];

    public bool IsPage { get; init; }

    public bool CarriesClerk { get; init; }

    public bool IsHeap { get; init; }

    public bool IsObjectCall { get; init; }

    public bool IsVirtual { get; init; }

    public int FlagSlot { get; init; } = -1;

    public ulong FlagMask { get; init; }

    public int FileSlot { get; init; } = -1;

    public int LineSlot { get; init; } = -1;

    public bool IsAllocation => Operation is AllocationOperation.Allocate or AllocationOperation.Reallocate;

    public static bool IsMemoryObjectClass(string? className)
        => className is not null && (className == MemoryObjectFactory || IsMemoryObject(className));

    public static bool IsAllocatorFrame(CallstackFrame? frame)
    {
        if (frame?.Resolved is not { } resolved)
        {
            return false;
        }

        if (resolved.ClassName is not { Length: > 0 } className)
        {
            return AllocatorFunctions.Contains(resolved.MethodName) || Classify(0, resolved.MethodName) is not null;
        }

        return IsMemoryObjectClass(className)
               || AllocatorClassPrefixes.Any(p => className.StartsWith(p, StringComparison.Ordinal))
               || Classify(0, $"{className}::{resolved.MethodName}") is not null;
    }

    public bool IsCounted(TraceArgumentCall call) => FlagSlot < 0 || (call.Slot(FlagSlot) & FlagMask) != 0;

    public ulong GetObject(TraceArgumentCall call) => Operation == AllocationOperation.Create ? call.ReturnedValue : call.Slot(0);

    public ulong GetPointer(TraceArgumentCall call) => call.Slot(PointerSlot);

    public string? GetSite(TraceArgumentCall call)
    {
        if (FileSlot < 0 || LineSlot < 0 || call.GetLocator(FileSlot) is not { ValueType: LocatorValueType.Text } locator)
        {
            return null;
        }

        var file = new TextLocator(locator);

        ulong? line = LineSlot < ArgumentLayout.RegisterCount ? call.Slot(LineSlot) : call.GetStackValue(LineSlot);

        if (file.IsTruncated || file.Text.Length == 0 || line is not { } number)
        {
            return null;
        }

        return $"{file.Text[(file.Text.LastIndexOfAny(PathSeparators) + 1)..]}:{(int)number}";
    }

    public ulong GetByteCount(TraceArgumentCall call)
    {
        if (SizeSlot < 0)
        {
            return IsAllocation ? Unit : 0;
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
                    => new(address, AllocationOperation.Allocate, 0, -1, 1),
                "operator delete" or "operator delete[]"
                    => new(address, AllocationOperation.Free, -1, 0, 1),
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
            => new(address, AllocationOperation.Allocate, 1, -1, 1)
            {
                IsPage = true,
                IsVirtual = true,
                FlagSlot = 2,
                FlagMask = MemCommit
            },
        "VirtualFree"
            => new(address, AllocationOperation.Free, -1, 0, 1)
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
            => new(address, AllocationOperation.Allocate, 2, -1, 1),
        "RtlReAllocateHeap" or "HeapReAlloc"
            => new(address, AllocationOperation.Reallocate, 3, 2, 1),
        "RtlFreeHeap" or "HeapFree"
            => new(address, AllocationOperation.Free, -1, 2, 1),
        "LocalAlloc" or "GlobalAlloc"
            => new(address, AllocationOperation.Allocate, 1, -1, 1),
        "LocalReAlloc" or "GlobalReAlloc"
            => new(address, AllocationOperation.Reallocate, 1, 0, 1),
        "LocalFree" or "GlobalFree"
            => new(address, AllocationOperation.Free, -1, 0, 1),
        "malloc" or "_aligned_malloc"
            => new(address, AllocationOperation.Allocate, 0, -1, 1),
        "calloc"
            => new(address, AllocationOperation.Allocate, 1, -1, 1, CountSlot: 0),
        "realloc" or "_aligned_realloc"
            => new(address, AllocationOperation.Reallocate, 1, 0, 1),
        "_recalloc"
            => new(address, AllocationOperation.Reallocate, 2, 0, 1, CountSlot: 1),
        "free" or "_aligned_free"
            => new(address, AllocationOperation.Free, -1, 0, 1),
        _ => null
    };

    private static bool IsMemoryObject(ReadOnlySpan<char> className)
        => className.StartsWith("CMemThread<", StringComparison.Ordinal) || MemoryObjectLookup.Contains(className);
}
