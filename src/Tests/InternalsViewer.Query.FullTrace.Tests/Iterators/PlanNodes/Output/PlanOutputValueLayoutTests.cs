using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes.Output;

[Trait("Category", "Unit")]
public class PlanOutputValueLayoutTests
{
    [Theory]
    [InlineData("CValHash", "Record Offset,Slot")]
    [InlineData("CValSSTable", "Slot,Accessor")]
    [InlineData("CValHashCachedSwitch", "Value,Cached Status,Cached Type,Cached Value,Cached Length")]
    [InlineData("CValExpr", "Executor")]
    [InlineData("CEsExec", "Input Values")]
    [InlineData("CValRefOrdinal", "")]
    public void A_Value_Object_Marks_The_Fields_Known_For_Its_Class(string className, string expected)
        => Assert.Equal(expected, string.Join(",", PlanOutputValueLayout.GetFields(className).Select(f => f.Name)));
}
