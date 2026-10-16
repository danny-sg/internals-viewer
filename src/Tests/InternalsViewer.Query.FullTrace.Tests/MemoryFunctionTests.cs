using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryFunctionTests
{
    [Theory]
    [InlineData("CMemThread<CMemObj>::Alloc", AllocationOperation.Allocate, 1, -1, 1ul)]
    [InlineData("CMemObj::Alloc", AllocationOperation.Allocate, 1, -1, 1ul)]
    [InlineData("CMemThread<CMemProc>::Realloc", AllocationOperation.Reallocate, 2, 1, 1ul)]
    [InlineData("CMemThread<CMemAligned<CMemObj> >::Free", AllocationOperation.Free, -1, 1, 1ul)]
    [InlineData("operator new", AllocationOperation.Allocate, 0, -1, 1ul)]
    [InlineData("operator delete[]", AllocationOperation.Free, -1, 0, 1ul)]
    [InlineData("MemoryClerkInternal::AllocatePages", AllocationOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("MemoryClerkInternal::FreePages", AllocationOperation.Free, 2, 1, 8192ul)]
    [InlineData("MemoryClerkInternal::AllocateReservedPages", AllocationOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("MemoryClerkInternal::FreeReservedPages", AllocationOperation.Free, 2, 1, 8192ul)]
    [InlineData("MemoryObjectFactory::CreateMemObject", AllocationOperation.Create, -1, -1, 1ul)]
    [InlineData("CQryMemManager::AllocatePages", AllocationOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("CQryMemManager::FreePages", AllocationOperation.Free, 2, 1, 8192ul)]
    [InlineData("CHashWorkfilePartitionInstance::PvAllocateHashBucketPage", AllocationOperation.Allocate, -1, -1, 8192ul)]
    [InlineData("CHashWorkfilePartitionInstance::FreeHashBucketPage", AllocationOperation.Free, -1, 1, 1ul)]
    public void An_Allocator_Is_Recognised_By_Its_Name(string symbol,
                                                       AllocationOperation operation,
                                                       int sizeSlot,
                                                       int pointerSlot,
                                                       ulong unit)
    {
        var function = MemoryFunction.Classify(0x1000, symbol);

        Assert.Equal((operation, sizeSlot, pointerSlot, unit),
                     (function!.Operation, function.SizeSlot, function.PointerSlot, function.Unit));
    }

    [Theory]
    [InlineData("CMemThread<CMemFixed>::FreeToMark")]
    [InlineData("CMemProcCache<CStrPtr,10>::AllocateMemory")]
    [InlineData("CQScanHashNew::Alloc")]
    [InlineData("CMemObj::Release")]
    public void Other_Functions_Are_Not_Allocators(string symbol) => Assert.Null(MemoryFunction.Classify(0x1000, symbol));

    [Fact]
    public void Bytes_Are_Read_From_The_Size_Argument()
    {
        var call = new TraceArgumentCall(At(1), 1, true, [0xA0, 3, 100, 0], 0);

        Assert.Equal(3ul, MemoryFunction.Classify(0x1000, "CMemObj::Alloc")!.GetByteCount(call));
        Assert.Equal(100ul, MemoryFunction.Classify(0x1000, "CMemObj::Realloc")!.GetByteCount(call));
        Assert.Equal(3ul * 8192, MemoryFunction.Classify(0x1000, "MemoryClerkInternal::AllocatePages")!.GetByteCount(call));
        Assert.Equal(0ul, MemoryFunction.Classify(0x1000, "CMemObj::Free")!.GetByteCount(call));
    }

    [Theory]
    [InlineData("RtlAllocateHeap", AllocationOperation.Allocate, 2, -1)]
    [InlineData("RtlReAllocateHeap", AllocationOperation.Reallocate, 3, 2)]
    [InlineData("RtlFreeHeap", AllocationOperation.Free, -1, 2)]
    [InlineData("LocalAlloc", AllocationOperation.Allocate, 1, -1)]
    [InlineData("malloc", AllocationOperation.Allocate, 0, -1)]
    [InlineData("realloc", AllocationOperation.Reallocate, 1, 0)]
    [InlineData("free", AllocationOperation.Free, -1, 0)]
    public void A_Heap_Function_Is_Recognised_By_Its_Export(string export, AllocationOperation operation, int sizeSlot, int pointerSlot)
    {
        var function = MemoryFunction.ClassifyExport(0x1000, export);

        Assert.Equal((operation, sizeSlot, pointerSlot), (function!.Operation, function.SizeSlot, function.PointerSlot));
    }

    [Fact]
    public void Calloc_Allocates_Its_Count_Times_Its_Size()
    {
        var call = new TraceArgumentCall(At(1), 1, true, [4, 24, 0, 0], 0);

        Assert.Equal(96ul, MemoryFunction.ClassifyExport(0x1000, "calloc")!.GetByteCount(call));
    }

    [Fact]
    public void Clerk_Pages_Carry_The_Clerk_And_Heap_Exports_Are_Heap()
    {
        var pages = MemoryFunction.Classify(0x1000, "MemoryClerkInternal::AllocatePages")!;

        var workspace = MemoryFunction.Classify(0x2000, "CQryMemManager::AllocatePages")!;

        var heap = MemoryFunction.ClassifyExport(0x3000, "RtlAllocateHeap")!;

        Assert.Equal((true, true, false), (pages.IsPage, pages.CarriesClerk, pages.IsHeap));
        Assert.Equal((true, false), (workspace.IsPage, workspace.CarriesClerk));
        Assert.True(heap.IsHeap);
        Assert.True(MemoryFunction.IsMemoryObjectClass("MemoryObjectFactory"));
        Assert.False(MemoryFunction.IsMemoryObjectClass("CQScanHash"));
    }

    [Fact]
    public void Virtual_Memory_Counts_Commits_And_Releases_Only()
    {
        var allocate = MemoryFunction.ClassifyExport(0x1000, "VirtualAlloc")!;

        var free = MemoryFunction.ClassifyExport(0x2000, "VirtualFree")!;

        var commit = new TraceArgumentCall(At(1), 1, true, [0, 65536, 0x3000, 4], 0x9000);

        var reserve = new TraceArgumentCall(At(2), 1, true, [0, 65536, 0x2000, 4], 0xA000);

        var release = new TraceArgumentCall(At(3), 1, true, [0x9000, 0, 0x8000, 0], 1);

        Assert.Equal((true, false, true), (allocate.IsCounted(commit), allocate.IsCounted(reserve), free.IsCounted(release)));
        Assert.Equal(65536ul, allocate.GetByteCount(commit));
        Assert.Equal((true, false), (allocate.IsVirtual, allocate.IsHeap));
    }

    [Fact]
    public void A_Clerk_Address_Is_Read_Big_Endian()
    {
        Assert.Equal(0x000001A2B3C40040ul, MemoryClerkReader.AddressOf([0x00, 0x00, 0x01, 0xA2, 0xB3, 0xC4, 0x00, 0x40]));
    }

    [Fact]
    public void A_Fixed_Size_Allocator_Allocates_Its_Unit()
    {
        var call = new TraceArgumentCall(At(1), 1, true, [0xA0, 0, 0, 0], 0x5000);

        var function = MemoryFunction.Classify(0x1000, "CHashWorkfilePartitionInstance::PvAllocateHashBucketPage")!;

        Assert.Equal(8192ul, function.GetByteCount(call));
    }

    [Fact]
    public void An_Impossible_Size_Counts_As_Nothing()
    {
        var call = new TraceArgumentCall(At(1), 1, true, [0xA0, ulong.MaxValue, 0, 0], 0);

        Assert.Equal(0ul, MemoryFunction.Classify(0x1000, "CMemObj::Alloc")!.GetByteCount(call));
    }
}
