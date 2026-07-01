using InternalsViewer.Internals.Engine.Allocation;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

public class BitmapAllocationTests
{
    private const short FileId = 1;

    private static byte[] BuildMap(int sizeInBytes, params int[] setBitIndexes)
    {
        var map = new byte[sizeInBytes];

        foreach (var bitIndex in setBitIndexes)
        {
            map[bitIndex >> 3] |= (byte)(1 << (bitIndex & 7));
        }

        return map;
    }

    [Fact]
    public void IsExtentAllocated_Returns_True_For_Allocated_Extent()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 5));

        Assert.True(allocation.IsExtentAllocated(5, FileId, isInverted: false));
    }

    [Fact]
    public void IsExtentAllocated_Returns_False_For_Unallocated_Extent()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 5));

        Assert.False(allocation.IsExtentAllocated(6, FileId, isInverted: false));
    }

    [Fact]
    public void IsExtentAllocated_Applies_StartOffset()
    {
        // startOffset 8 shifts the bit index into the second byte, so extent 0 maps to bit index 8.
        var allocation = new BitmapAllocation(FileId, startOffset: 8, BuildMap(2, 8));

        Assert.True(allocation.IsExtentAllocated(0, FileId, isInverted: false));
        Assert.False(allocation.IsExtentAllocated(1, FileId, isInverted: false));
    }

    [Fact]
    public void IsExtentAllocated_Returns_IsInverted_When_File_Mismatch()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 5));

        Assert.False(allocation.IsExtentAllocated(5, fileId: 2, isInverted: false));
        Assert.True(allocation.IsExtentAllocated(5, fileId: 2, isInverted: true));
    }

    [Fact]
    public void IsExtentAllocated_Inverted_Negates_Result()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 5));

        Assert.False(allocation.IsExtentAllocated(5, FileId, isInverted: true));
        Assert.True(allocation.IsExtentAllocated(6, FileId, isInverted: true));
    }

    [Fact]
    public void AnyExtentsAllocated_Returns_True_When_Any_In_Range_Allocated()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 2));

        Assert.True(allocation.AnyExtentsAllocated(0, 3, FileId, isInverted: false));
    }

    [Fact]
    public void AnyExtentsAllocated_Returns_False_When_None_In_Range_Allocated()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2));

        Assert.False(allocation.AnyExtentsAllocated(0, 3, FileId, isInverted: false));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_Returns_True_When_Any_In_Range_Unallocated()
    {
        // 0x0F sets extents 0-3; extent 4 is unallocated so inverted search finds it.
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 0, 1, 2, 3));

        Assert.True(allocation.AnyExtentsAllocated(0, 4, FileId, isInverted: true));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_Returns_False_When_All_In_Range_Allocated()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 0, 1, 2, 3));

        Assert.False(allocation.AnyExtentsAllocated(0, 3, FileId, isInverted: true));
    }

    [Fact]
    public void AnyExtentsAllocated_Returns_IsInverted_When_File_Mismatch()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2, 0, 1, 2, 3));

        Assert.False(allocation.AnyExtentsAllocated(0, 3, fileId: 2, isInverted: false));
        Assert.True(allocation.AnyExtentsAllocated(0, 3, fileId: 2, isInverted: true));
    }

    [Fact]
    public void SinglePageSlots_Is_Empty()
    {
        var allocation = new BitmapAllocation(FileId, startOffset: 0, BuildMap(2));

        Assert.Empty(allocation.SinglePageSlots);
    }

    [Fact]
    public void Exposes_FileId_And_Allocations()
    {
        var map = BuildMap(2, 1);

        var allocation = new BitmapAllocation(FileId, startOffset: 0, map);

        Assert.Equal(FileId, allocation.FileId);
        Assert.Same(map, allocation.Allocations);
    }
}
