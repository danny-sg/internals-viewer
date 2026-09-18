using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.Controls.Timeline;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline;

public class ObjectPoolReadLinksTests
{
    [Fact]
    public void Gives_A_Read_The_Timeline_Index_Of_Its_Lookup()
    {
        var miss = new ObjectPoolEvent();

        var offTimeline = new ObjectPoolEvent();

        var linked = new ReadEventGroup { Events = [], PoolLookup = miss };

        var unlinked = new ReadEventGroup { Events = [] };

        var missing = new ReadEventGroup { Events = [], PoolLookup = offTimeline };

        var links = new ObjectPoolReadLinks();

        links.Rebuild(new EngineEvent[] { linked, miss, unlinked, missing });

        Assert.Equal(1, links.PoolIndexOf(0));
        Assert.Equal(-1, links.PoolIndexOf(1));
        Assert.Equal(-1, links.PoolIndexOf(2));
        Assert.Equal(-1, links.PoolIndexOf(3));
    }
}
