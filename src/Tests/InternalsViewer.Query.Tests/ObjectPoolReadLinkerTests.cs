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
    public void Link_Gives_An_Allocation_Page_Read_To_The_Next_Miss_On_Its_Task()
    {
        var iamAddress = new PageAddress(1, 500);

        var unit = new AllocationUnit { AllocationUnitId = 7 };

        unit.IamChain.Pages.Add(new IamPage { PageAddress = iamAddress });

        var dataRead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 501)], AllocationUnit = unit, SequenceId = 1, TaskAddress = 1 };

        var iamRead = new ReadEventGroup { Events = [], Pages = [iamAddress], SequenceId = 2, TaskAddress = 1 };

        var pfsRead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 8088), new PageAddress(1, 8089)], SequenceId = 3, TaskAddress = 1 };

        var hit = new ObjectPoolEvent { IsHit = true, SequenceId = 4, TaskAddress = 1 };

        var otherTaskMiss = new ObjectPoolEvent { IsHit = false, SequenceId = 5, TaskAddress = 2 };

        var miss = new ObjectPoolEvent { IsHit = false, SequenceId = 6, TaskAddress = 1 };

        ObjectPoolReadLinker.Link(new EngineEvent[] { dataRead, iamRead, pfsRead, hit, otherTaskMiss, miss });

        Assert.Null(dataRead.PoolLookup);
        Assert.Same(miss, iamRead.PoolLookup);
        Assert.Same(miss, pfsRead.PoolLookup);
    }
}
