using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Documents;

public class TraceConditionsTests
{
    [Fact]
    public void Parse_CallerConditions_NeedNoLayout()
    {
        var conditions = TraceConditions.Parse(["calledby SearchPage", "not operator Seek"], null);

        Assert.Equal(TraceConditionTarget.CalledBy, conditions[0].Target);
        Assert.Equal(TraceConditionOperator.Equal, conditions[0].Operator);
        Assert.Equal("SearchPage", conditions[0].Value);
        Assert.Equal(TraceConditionTarget.Operator, conditions[1].Target);
        Assert.Equal(TraceConditionOperator.NotEqual, conditions[1].Operator);
    }

    [Fact]
    public void Parse_ValueCondition_WithoutLayout_Throws()
    {
        Assert.True(TraceConditions.NeedsLayout(["arg 1 = 5"]));
        Assert.Throws<McpException>(() => TraceConditions.Parse(["arg 1 = 5"], null));
    }

    [Fact]
    public void Parse_Nonsense_Throws()
    {
        Assert.Throws<McpException>(() => TraceConditions.Parse(["banana"], null));
    }
}
