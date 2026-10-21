using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryIndexTests
{
    [Fact]
    public void Memory_In_Use_Rises_With_Allocations_And_Falls_With_Frees()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        Assert.Equal([0ul, 100ul, 150ul, 50ul], [index.PeakInUseDuring(5, 5), index.PeakInUseDuring(10, 10), index.PeakInUseDuring(25, 25), index.PeakInUseDuring(40, 40)]);
        Assert.Equal(150ul, index.PeakInUseDuring(0, 40));
        Assert.Equal(100ul, index.PeakInUseDuring(12, 18));
    }

    [Fact]
    public void Workspace_Memory_Is_Tracked_Apart_From_Everything_In_Use()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA, Workspace: true), new(7, 20, 21, 50, 0xB)],
                                      [new(7, 30, 31, 0xA)]);

        Assert.Equal((150ul, 100ul, 0ul),
                     (index.PeakInUseDuring(0, 40), index.PeakWorkspaceDuring(0, 40), index.PeakWorkspaceDuring(31, 40)));
    }

    [Fact]
    public void Memory_In_Use_Within_A_Call_Counts_Only_What_It_Allocated()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        var call = index.InUseWithin(7, 0, 40);

        Assert.Equal([0ul, 100ul, 150ul, 50ul],
                     [call.PeakDuring(0, 5), call.PeakDuring(10, 15), call.PeakDuring(15, 25), call.PeakDuring(31, 40)]);

        var later = index.InUseWithin(7, 15, 40);

        Assert.Equal([0ul, 50ul, 50ul], [later.PeakDuring(15, 18), later.PeakDuring(20, 25), later.PeakDuring(30, 40)]);
    }

    [Fact]
    public void An_Allocation_Without_Its_Pointer_Is_Never_In_Use()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0), new(7, 20, 21, 50, 0xB)], []);

        var call = index.InUseWithin(7, 0, 40);

        Assert.Equal((0ul, 50ul), (call.PeakDuring(10, 15), call.PeakDuring(25, 40)));
        Assert.Equal(50ul, index.RetainedBy(7, 0, 40));
    }

    [Fact]
    public void A_Free_Releases_Memory_Allocated_On_Another_Thread()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA)], [new(9, 30, 31, 0xA)]);

        Assert.Equal(100ul, index.FreedDuring(9, 0, 40));
        Assert.Equal(0ul, index.FreedDuring(7, 0, 40));
        Assert.Equal(0ul, index.PeakInUseDuring(35, 35));
    }

    [Fact]
    public void A_Call_Retains_What_It_Allocated_And_Did_Not_Free_By_Its_End()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        Assert.Equal(150ul, index.RetainedBy(7, 0, 25));
        Assert.Equal(50ul, index.RetainedBy(7, 0, 40));
        Assert.Equal(100ul, index.FreedDuring(7, 0, 40));
    }

    [Fact]
    public void A_Free_Of_Memory_From_Before_The_Recording_Changes_Nothing()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA)], [new(7, 30, 31, 0xF)]);

        Assert.Equal(100ul, index.PeakInUseDuring(40, 40));
        Assert.Equal(0ul, index.FreedDuring(7, 0, 40));
    }

    [Fact]
    public void An_Allocation_Inside_Another_Is_Counted_Once()
    {
        var index = MemoryIndex.Build([new(7, 10, 20, 8192, 0xA), new(7, 12, 13, 8192, 0xA)],
                                      [new(7, 30, 40, 0xA), new(7, 32, 33, 0xA)]);

        Assert.Equal((8192ul, 1), index.AllocatedDuring(7, 0, 50));
        Assert.Equal(0ul, index.PeakInUseDuring(45, 45));
    }

    [Fact]
    public void Memory_Allocated_During_A_Range_Is_The_Sum_Of_The_Allocations_Inside_It()
    {
        var index = MemoryIndex.Build([new(7, 10, 15, 100, 0xA), new(7, 12, 13, 40, 0xB), new(7, 20, 25, 50, 0xC), new(9, 10, 11, 5, 0xD)],
                                      []);

        Assert.Equal((150ul, 2), index.AllocatedDuring(7, 0, 30));
        Assert.Equal((50ul, 1), index.AllocatedDuring(7, 15, 30));
        Assert.Equal((100ul, 1), index.AllocatedDuring(7, 10, 15));
        Assert.Equal((0ul, 0), index.AllocatedDuring(8, 0, 30));
    }

    [Fact]
    public void A_Reallocation_In_Place_Frees_Before_It_Allocates()
    {
        var index = MemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 200, 0xA)], [new(7, 20, 21, 0xA)]);

        Assert.Equal(200ul, index.PeakInUseDuring(25, 25));
    }
}
