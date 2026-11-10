using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans;

[Trait("Category", "Unit")]
public class PlanNodePredicateTextTests
{
    [Fact]
    public void A_Value_That_Did_Not_Parse_Shows_The_Plan_Text()
    {
        var value = new DefinedValueInfo { Expression = "getdate()" };

        Assert.Equal("getdate()", value.GetText(null).ToString());
    }

    [Fact]
    public void Several_Values_Are_Listed_With_Commas()
    {
        DefinedValueInfo[] values = [new() { Expression = "first" }, new() { Expression = "second" }];

        Assert.Equal("first, second", values.GetText(null).ToString());
    }
}
