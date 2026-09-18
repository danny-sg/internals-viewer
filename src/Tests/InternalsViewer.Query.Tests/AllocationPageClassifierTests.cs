using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class AllocationPageClassifierTests
{
    [Fact]
    public void Classify_Marks_Reads_Of_Iam_And_Fixed_Allocation_Pages()
    {
        var iamAddress = new PageAddress(1, 500);

        var unit = new AllocationUnit { AllocationUnitId = 7 };

        unit.IamChain.Pages.Add(new IamPage { PageAddress = iamAddress });

        var dataRead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 501)], AllocationUnit = unit };

        var iamRead = new ReadEventGroup { Events = [], Pages = [iamAddress] };

        var pfsRead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 8088), new PageAddress(1, 8089)] };

        AllocationPageClassifier.Classify(new EngineEvent[] { dataRead, iamRead, pfsRead });

        Assert.False(dataRead.IsAllocationPage);
        Assert.True(iamRead.IsAllocationPage);
        Assert.True(pfsRead.IsAllocationPage);
    }
}
