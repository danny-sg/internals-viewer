using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelMemoryIndexTests
{
    [Fact]
    public void Memory_In_Use_Rises_With_Allocations_And_Falls_With_Frees()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        Assert.Equal([0ul, 100ul, 150ul, 50ul], [index.PeakInUseDuring(5, 5), index.PeakInUseDuring(10, 10), index.PeakInUseDuring(25, 25), index.PeakInUseDuring(40, 40)]);
        Assert.Equal(150ul, index.PeakInUseDuring(0, 40));
        Assert.Equal(100ul, index.PeakInUseDuring(12, 18));
    }

    [Fact]
    public void Workspace_Memory_Is_Tracked_Apart_From_Everything_In_Use()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA, Workspace: true), new(7, 20, 21, 50, 0xB)],
                                                [new(7, 30, 31, 0xA)]);

        Assert.Equal((150ul, 100ul, 0ul),
                     (index.PeakInUseDuring(0, 40), index.PeakWorkspaceDuring(0, 40), index.PeakWorkspaceDuring(31, 40)));
    }

    [Fact]
    public void Memory_In_Use_Within_A_Call_Counts_Only_What_It_Allocated()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        var call = index.InUseWithin(7, 0, 40);

        Assert.Equal([0ul, 100ul, 150ul, 50ul],
                     [call.PeakDuring(0, 5), call.PeakDuring(10, 15), call.PeakDuring(15, 25), call.PeakDuring(31, 40)]);

        var later = index.InUseWithin(7, 15, 40);

        Assert.Equal([0ul, 50ul, 50ul], [later.PeakDuring(15, 18), later.PeakDuring(20, 25), later.PeakDuring(30, 40)]);
    }

    [Fact]
    public void An_Allocation_Without_Its_Pointer_Is_Never_In_Use()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0), new(7, 20, 21, 50, 0xB)], []);

        var call = index.InUseWithin(7, 0, 40);

        Assert.Equal((0ul, 50ul), (call.PeakDuring(10, 15), call.PeakDuring(25, 40)));
        Assert.Equal(50ul, index.RetainedBy(7, 0, 40));
    }

    [Fact]
    public void A_Free_Releases_Memory_Allocated_On_Another_Thread()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA)], [new(9, 30, 31, 0xA)]);

        Assert.Equal(100ul, index.FreedDuring(9, 0, 40));
        Assert.Equal(0ul, index.FreedDuring(7, 0, 40));
        Assert.Equal(0ul, index.PeakInUseDuring(35, 35));
    }

    [Fact]
    public void A_Call_Retains_What_It_Allocated_And_Did_Not_Free_By_Its_End()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 50, 0xB)], [new(7, 30, 31, 0xA)]);

        Assert.Equal(150ul, index.RetainedBy(7, 0, 25));
        Assert.Equal(50ul, index.RetainedBy(7, 0, 40));
        Assert.Equal(100ul, index.FreedDuring(7, 0, 40));
    }

    [Fact]
    public void A_Free_Of_Memory_From_Before_The_Recording_Changes_Nothing()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA)], [new(7, 30, 31, 0xF)]);

        Assert.Equal(100ul, index.PeakInUseDuring(40, 40));
        Assert.Equal(0ul, index.FreedDuring(7, 0, 40));
    }

    [Fact]
    public void An_Allocation_Inside_Another_Is_Counted_Once()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 20, 8192, 0xA), new(7, 12, 13, 8192, 0xA)],
                                                [new(7, 30, 40, 0xA), new(7, 32, 33, 0xA)]);

        Assert.Equal((8192ul, 1), index.AllocatedDuring(7, 0, 50));
        Assert.Equal(0ul, index.PeakInUseDuring(45, 45));
    }

    [Fact]
    public void A_Reallocation_In_Place_Frees_Before_It_Allocates()
    {
        var index = TimeTravelMemoryIndex.Build([new(7, 10, 11, 100, 0xA), new(7, 20, 21, 200, 0xA)], [new(7, 20, 21, 0xA)]);

        Assert.Equal(200ul, index.PeakInUseDuring(25, 25));
    }
}
