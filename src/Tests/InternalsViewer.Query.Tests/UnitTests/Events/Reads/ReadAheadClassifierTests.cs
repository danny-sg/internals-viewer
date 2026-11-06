using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests.UnitTests.Events.Reads;

public class ReadAheadClassifierTests
{
    [Fact]
    public void A_Read_Of_More_Than_One_Page_Is_A_Read_Ahead()
    {
        var readAhead = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 336), new PageAddress(1, 337)] };

        var single = new ReadEventGroup { Events = [], Pages = [new PageAddress(1, 336)] };

        ReadAheadClassifier.Classify([readAhead, single]);

        Assert.Equal((true, false), (readAhead.IsReadAhead, single.IsReadAhead));
    }
}
