using InternalsViewer.Query.FullTrace.Memory;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryIndexTests
{
    [Fact]
    public void Memory_In_Use_Rises_With_Allocations_And_Falls_With_Frees()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA), new(7, At(20), At(21), 50, 0xB)], [new(7, At(30), At(31), 0xA)]);

        Assert.Equal([0ul, 100ul, 150ul, 50ul],
                     [index.PeakInUseDuring(At(5), At(5)),
                      index.PeakInUseDuring(At(10), At(10)),
                      index.PeakInUseDuring(At(25), At(25)),
                      index.PeakInUseDuring(At(40), At(40))]);
        Assert.Equal(150ul, index.PeakInUseDuring(At(0), At(40)));
        Assert.Equal(100ul, index.PeakInUseDuring(At(12), At(18)));
    }

    [Fact]
    public void Workspace_Memory_Is_Tracked_Apart_From_Everything_In_Use()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA, Workspace: true), new(7, At(20), At(21), 50, 0xB)],
                                      [new(7, At(30), At(31), 0xA)]);

        Assert.Equal((150ul, 100ul, 0ul),
                     (index.PeakInUseDuring(At(0), At(40)),
                      index.PeakWorkspaceDuring(At(0), At(40)),
                      index.PeakWorkspaceDuring(At(31), At(40))));
    }

    [Fact]
    public void Memory_In_Use_Within_A_Call_Counts_Only_What_It_Allocated()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA), new(7, At(20), At(21), 50, 0xB)], [new(7, At(30), At(31), 0xA)]);

        var call = index.InUseWithin(7, At(0), At(40));

        Assert.Equal([0ul, 100ul, 150ul, 50ul],
                     [call.PeakDuring(At(0), At(5)),
                      call.PeakDuring(At(10), At(15)),
                      call.PeakDuring(At(15), At(25)),
                      call.PeakDuring(At(31), At(40))]);

        var later = index.InUseWithin(7, At(15), At(40));

        Assert.Equal([0ul, 50ul, 50ul],
                     [later.PeakDuring(At(15), At(18)), later.PeakDuring(At(20), At(25)), later.PeakDuring(At(30), At(40))]);
    }

    [Fact]
    public void An_Allocation_Without_Its_Pointer_Is_Never_In_Use()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0), new(7, At(20), At(21), 50, 0xB)], []);

        var call = index.InUseWithin(7, At(0), At(40));

        Assert.Equal((0ul, 50ul), (call.PeakDuring(At(10), At(15)), call.PeakDuring(At(25), At(40))));
        Assert.Equal(50ul, index.RetainedBy(7, At(0), At(40)));
    }

    [Fact]
    public void A_Free_Releases_Memory_Allocated_On_Another_Thread()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA)], [new(9, At(30), At(31), 0xA)]);

        Assert.Equal(100ul, index.FreedDuring(9, At(0), At(40)));
        Assert.Equal(0ul, index.FreedDuring(7, At(0), At(40)));
        Assert.Equal(0ul, index.PeakInUseDuring(At(35), At(35)));
    }

    [Fact]
    public void A_Call_Retains_What_It_Allocated_And_Did_Not_Free_By_Its_End()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA), new(7, At(20), At(21), 50, 0xB)], [new(7, At(30), At(31), 0xA)]);

        Assert.Equal(150ul, index.RetainedBy(7, At(0), At(25)));
        Assert.Equal(50ul, index.RetainedBy(7, At(0), At(40)));
        Assert.Equal(100ul, index.FreedDuring(7, At(0), At(40)));
    }

    [Fact]
    public void A_Free_Of_Memory_From_Before_The_Recording_Changes_Nothing()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA)], [new(7, At(30), At(31), 0xF)]);

        Assert.Equal(100ul, index.PeakInUseDuring(At(40), At(40)));
        Assert.Equal(0ul, index.FreedDuring(7, At(0), At(40)));
    }

    [Fact]
    public void An_Allocation_Inside_Another_Is_Counted_Once()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(20), 8192, 0xA), new(7, At(12), At(13), 8192, 0xA)],
                                      [new(7, At(30), At(40), 0xA), new(7, At(32), At(33), 0xA)]);

        Assert.Equal((8192ul, 1), index.AllocatedDuring(7, At(0), At(50)));
        Assert.Equal(0ul, index.PeakInUseDuring(At(45), At(45)));
    }

    [Fact]
    public void Memory_Allocated_During_A_Range_Is_The_Sum_Of_The_Allocations_Inside_It()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(15), 100, 0xA),
                                       new(7, At(12), At(13), 40, 0xB),
                                       new(7, At(20), At(25), 50, 0xC),
                                       new(9, At(10), At(11), 5, 0xD)],
                                      []);

        Assert.Equal((150ul, 2), index.AllocatedDuring(7, At(0), At(30)));
        Assert.Equal((50ul, 1), index.AllocatedDuring(7, At(15), At(30)));
        Assert.Equal((100ul, 1), index.AllocatedDuring(7, At(10), At(15)));
        Assert.Equal((0ul, 0), index.AllocatedDuring(8, At(0), At(30)));
    }

    [Fact]
    public void A_Reallocation_In_Place_Frees_Before_It_Allocates()
    {
        var index = MemoryIndex.Build([new(7, At(10), At(11), 100, 0xA), new(7, At(20), At(21), 200, 0xA)], [new(7, At(20), At(21), 0xA)]);

        Assert.Equal(200ul, index.PeakInUseDuring(At(25), At(25)));
    }
}
