using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class ObjectPoolReadLinkerTests
{
    [Fact]
    public void Link_Gives_A_Read_To_The_Next_Lookup_That_Owns_Its_Page()
    {
        var page = new PageAddress(1, 10);

        var earlier = new ObjectPoolEvent { Pages = [page], SequenceId = 1, TaskAddress = 1 };

        var read = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 2, TaskAddress = 1 };

        var next = new ObjectPoolEvent { Pages = [page], SequenceId = 3, TaskAddress = 1 };

        var lateRead = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 4, TaskAddress = 1 };

        var unowned = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 99)], SequenceId = 5, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { earlier, read, next, lateRead, unowned });

        Assert.Same(next, read.PoolLookup);
        Assert.Same(next, lateRead.PoolLookup);
        Assert.Null(unowned.PoolLookup);
    }

    [Fact]
    public void Link_Gives_Pages_Dragged_In_By_An_Extent_Read_To_The_Object_Being_Built()
    {
        var previousObjectTail = new[] { new PageAddress(1, 13504), new PageAddress(1, 13505) };

        var buildingHead = new PageAddress(1, 13506);

        var collateral = new ReadEventGroup { Events = [], Pages = previousObjectTail, ReadType = ReadType.NonCached, SequenceId = 1, TaskAddress = 1 };

        var wanted = new ReadEventGroup { Events = [], Pages = [buildingHead], ReadType = ReadType.NonCached, SequenceId = 2, TaskAddress = 1 };

        var building = new ObjectPoolEvent { IsHit = false, Pages = [buildingHead], SequenceId = 3, TaskAddress = 1 };

        var readAhead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 13400)], ReadType = ReadType.NonCached, SequenceId = 4, TaskAddress = 1 };

        var later = new ObjectPoolEvent { IsHit = false, Pages = [.. previousObjectTail, new PageAddress(1, 13400)], SequenceId = 5, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { collateral, wanted, building, readAhead, later });

        Assert.Same(building, wanted.PoolLookup);
        Assert.Same(building, collateral.PoolLookup);
        Assert.Same(later, readAhead.PoolLookup);
    }

    [Fact]
    public void Link_Leaves_Allocation_Page_Reads_Unlinked()
    {
        var page = new PageAddress(1, 10);

        var allocation = new ReadEventGroup { Events = [], Pages = [page], IsAllocationPage = true, SequenceId = 1, TaskAddress = 1 };

        var miss = new ObjectPoolEvent { IsHit = false, Pages = [page], SequenceId = 2, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { allocation, miss });

        Assert.Null(allocation.PoolLookup);
        Assert.Null(allocation.ReadAheadFor);
    }

    [Fact]
    public void Link_Leaves_A_Read_Ahead_Read_Unlinked()
    {
        var page = new PageAddress(1, 10);

        var read = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 1, TaskAddress = 1, IsReadAhead = true };

        var miss = new ObjectPoolEvent { Pages = [page], SequenceId = 2, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { read, miss });

        Assert.Null(read.PoolLookup);
        Assert.Same(miss, read.ReadAheadFor);
    }

    [Fact]
    public void Link_Treats_A_Read_Before_An_Earlier_Lookup_As_Read_Ahead()
    {
        var page = new PageAddress(1, 10);

        var fetched = new ReadEventGroup { Events = [], Pages = [page], ReadType = ReadType.NonCached, SequenceId = 1, TaskAddress = 1 };

        var cached = new ReadEventGroup { Events = [], Pages = [page], ReadType = ReadType.Cached, SequenceId = 2, TaskAddress = 1 };

        var earlier = new ObjectPoolEvent { Pages = [new PageAddress(1, 99)], SequenceId = 3, TaskAddress = 1 };

        var build = new ReadEventGroup { Events = [], Pages = [page], ReadType = ReadType.Cached, SequenceId = 4, TaskAddress = 1 };

        var owner = new ObjectPoolEvent { Pages = [page], SequenceId = 5, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { fetched, cached, earlier, build, owner });

        Assert.True(fetched.IsReadAhead);
        Assert.Null(fetched.PoolLookup);
        Assert.Same(owner, fetched.ReadAheadFor);
        Assert.True(cached.IsReadAhead);
        Assert.Same(owner, cached.ReadAheadFor);
        Assert.Same(owner, build.PoolLookup);
    }

    [Fact]
    public void Link_Treats_A_Read_Overtaken_By_A_Later_Objects_Read_As_Read_Ahead()
    {
        var columnTwo = new PageAddress(1, 10);

        var columnThree = new PageAddress(1, 20);

        var prefetchTwo = new ReadEventGroup { Events = [], Pages = [columnTwo], ReadType = ReadType.NonCached, SequenceId = 1, TaskAddress = 1 };

        var prefetchThree = new ReadEventGroup { Events = [], Pages = [columnThree], ReadType = ReadType.NonCached, SequenceId = 2, TaskAddress = 1 };

        var buildTwo = new ReadEventGroup { Events = [], Pages = [columnTwo], ReadType = ReadType.Cached, SequenceId = 3, TaskAddress = 1 };

        var missTwo = new ObjectPoolEvent { Pages = [columnTwo], SequenceId = 4, TaskAddress = 1 };

        var buildThree = new ReadEventGroup { Events = [], Pages = [columnThree], ReadType = ReadType.Cached, SequenceId = 5, TaskAddress = 1 };

        var missThree = new ObjectPoolEvent { Pages = [columnThree], SequenceId = 6, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { prefetchTwo, prefetchThree, buildTwo, missTwo, buildThree, missThree });

        Assert.True(prefetchTwo.IsReadAhead);
        Assert.Same(missTwo, prefetchTwo.ReadAheadFor);
        Assert.True(prefetchThree.IsReadAhead);
        Assert.Same(missThree, prefetchThree.ReadAheadFor);
        Assert.Same(missTwo, buildTwo.PoolLookup);
        Assert.Same(missThree, buildThree.PoolLookup);
    }
}
