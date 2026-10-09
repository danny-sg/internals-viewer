using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class MemoryFunctionTests
{
    [Theory]
    [InlineData("CMemThread<CMemObj>::Alloc", MemoryOperation.Allocate, 1, -1, 1ul)]
    [InlineData("CMemObj::Alloc", MemoryOperation.Allocate, 1, -1, 1ul)]
    [InlineData("CMemThread<CMemProc>::Realloc", MemoryOperation.Reallocate, 2, 1, 1ul)]
    [InlineData("CMemThread<CMemAligned<CMemObj> >::Free", MemoryOperation.Free, -1, 1, 1ul)]
    [InlineData("operator new", MemoryOperation.Allocate, 0, -1, 1ul)]
    [InlineData("operator delete[]", MemoryOperation.Free, -1, 0, 1ul)]
    [InlineData("MemoryClerkInternal::AllocatePages", MemoryOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("MemoryClerkInternal::FreePages", MemoryOperation.Free, 2, 1, 8192ul)]
    [InlineData("MemoryClerkInternal::AllocateReservedPages", MemoryOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("MemoryClerkInternal::FreeReservedPages", MemoryOperation.Free, 2, 1, 8192ul)]
    [InlineData("MemoryObjectFactory::CreateMemObject", MemoryOperation.Create, -1, -1, 1ul)]
    [InlineData("CQryMemManager::AllocatePages", MemoryOperation.Allocate, 1, -1, 8192ul)]
    [InlineData("CQryMemManager::FreePages", MemoryOperation.Free, 2, 1, 8192ul)]
    [InlineData("CHashWorkfilePartitionInstance::PvAllocateHashBucketPage", MemoryOperation.Allocate, -1, -1, 8192ul)]
    [InlineData("CHashWorkfilePartitionInstance::FreeHashBucketPage", MemoryOperation.Free, -1, 1, 1ul)]
    public void An_Allocator_Is_Recognised_By_Its_Name(string symbol,
                                                       MemoryOperation operation,
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
        var call = new TimeTravelArgumentCall(1, 1, true, [0xA0, 3, 100, 0], 0);

        Assert.Equal(3ul, MemoryFunction.Classify(0x1000, "CMemObj::Alloc")!.BytesOf(call));
        Assert.Equal(100ul, MemoryFunction.Classify(0x1000, "CMemObj::Realloc")!.BytesOf(call));
        Assert.Equal(3ul * 8192, MemoryFunction.Classify(0x1000, "MemoryClerkInternal::AllocatePages")!.BytesOf(call));
        Assert.Equal(0ul, MemoryFunction.Classify(0x1000, "CMemObj::Free")!.BytesOf(call));
    }

    [Theory]
    [InlineData("RtlAllocateHeap", MemoryOperation.Allocate, 2, -1)]
    [InlineData("RtlReAllocateHeap", MemoryOperation.Reallocate, 3, 2)]
    [InlineData("RtlFreeHeap", MemoryOperation.Free, -1, 2)]
    [InlineData("LocalAlloc", MemoryOperation.Allocate, 1, -1)]
    [InlineData("malloc", MemoryOperation.Allocate, 0, -1)]
    [InlineData("realloc", MemoryOperation.Reallocate, 1, 0)]
    [InlineData("free", MemoryOperation.Free, -1, 0)]
    public void A_Heap_Function_Is_Recognised_By_Its_Export(string export, MemoryOperation operation, int sizeSlot, int pointerSlot)
    {
        var function = MemoryFunction.ClassifyExport(0x1000, export);

        Assert.Equal((operation, sizeSlot, pointerSlot), (function!.Operation, function.SizeSlot, function.PointerSlot));
    }

    [Fact]
    public void Calloc_Allocates_Its_Count_Times_Its_Size()
    {
        var call = new TimeTravelArgumentCall(1, 1, true, [4, 24, 0, 0], 0);

        Assert.Equal(96ul, MemoryFunction.ClassifyExport(0x1000, "calloc")!.BytesOf(call));
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

        var commit = new TimeTravelArgumentCall(1, 1, true, [0, 65536, 0x3000, 4], 0x9000);

        var reserve = new TimeTravelArgumentCall(2, 1, true, [0, 65536, 0x2000, 4], 0xA000);

        var release = new TimeTravelArgumentCall(3, 1, true, [0x9000, 0, 0x8000, 0], 1);

        Assert.Equal((true, false, true), (allocate.Applies(commit), allocate.Applies(reserve), free.Applies(release)));
        Assert.Equal(65536ul, allocate.BytesOf(commit));
        Assert.Equal((true, false), (allocate.IsVirtual, allocate.IsHeap));
    }

    [Fact]
    public void A_Clerk_Address_Is_Read_Big_Endian()
    {
        Assert.Equal(0x000001A2B3C40040ul, MemoryClerks.AddressOf([0x00, 0x00, 0x01, 0xA2, 0xB3, 0xC4, 0x00, 0x40]));
    }

    [Fact]
    public void A_Fixed_Size_Allocator_Allocates_Its_Unit()
    {
        var call = new TimeTravelArgumentCall(1, 1, true, [0xA0, 0, 0, 0], 0x5000);

        Assert.Equal(8192ul, MemoryFunction.Classify(0x1000, "CHashWorkfilePartitionInstance::PvAllocateHashBucketPage")!.BytesOf(call));
    }

    [Fact]
    public void An_Impossible_Size_Counts_As_Nothing()
    {
        var call = new TimeTravelArgumentCall(1, 1, true, [0xA0, ulong.MaxValue, 0, 0], 0);

        Assert.Equal(0ul, MemoryFunction.Classify(0x1000, "CMemObj::Alloc")!.BytesOf(call));
    }
}
