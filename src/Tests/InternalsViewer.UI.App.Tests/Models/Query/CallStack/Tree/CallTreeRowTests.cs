using InternalsViewer.UI.App.Models.Query.CallStack.Tree;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack.Tree;

public class CallTreeRowTests
{
    [Fact]
    public void Walk_Visits_Every_Row_Depth_First_From_The_Last_Root()
    {
        var first = new CallTreeRow("first");
        var second = new CallTreeRow("second");

        first.Add(new CallTreeRow("first child"));
        second.Add(new CallTreeRow("second child"));

        Assert.Equal(["second", "second child", "first", "first child"], CallTreeRow.Walk([first, second]).Select(r => r.Content));
    }
}
