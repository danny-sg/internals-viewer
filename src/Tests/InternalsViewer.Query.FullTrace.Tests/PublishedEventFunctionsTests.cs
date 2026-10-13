using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class PublishedEventFunctionsTests
{
    [Theory]
    [InlineData("XeSqlPkg::lock_acquired", "Publish", "lock_acquired")]
    [InlineData("XeCloudMinPkg::rg_telemetry_tags", "Publish", "rg_telemetry_tags")]
    [InlineData("XeSqlPkg::lock_acquired", "Fire", null)]
    [InlineData("XeSqlPkg::lock_acquired::sm_contexts", "Publish", null)]
    [InlineData("lock_acquired", "Publish", null)]
    public void GetPublishEventName_Names_The_Event_Of_A_Package_Event_Class_Publish(string className, string methodName, string? expected)
        => Assert.Equal(expected, PublishedEventFunctions.GetPublishEventName(className, methodName));

    [Theory]
    [InlineData("XE_BufferMgr", "Reserve", true)]
    [InlineData("XE_BufferMgr", "Release", false)]
    [InlineData("XeSqlPkg::lock_acquired", "Reserve", false)]
    public void IsEventWriter_Is_The_Session_Buffer_Reserve(string className, string methodName, bool expected)
        => Assert.Equal(expected, PublishedEventFunctions.IsEventWriter(className, methodName));

    [Fact]
    public void From_Takes_Publishers_And_Writers_From_The_Function_Classifications()
    {
        var functions = new Dictionary<uint, FunctionClassification>
        {
            [0x10] = Classification(publishEventName: "latch_acquired"),
            [0x20] = Classification(eventWriter: true),
            [0x30] = Classification()
        };

        var published = PublishedEventFunctions.From(functions);

        Assert.Equal([new EventPublisher(0x10, "latch_acquired")], published.Publishers);
        Assert.Equal([0x20ul], published.Writers);
    }

    private static FunctionClassification Classification(bool eventWriter = false, string? publishEventName = null)
        => new(false, false, false, eventWriter, publishEventName, null);
}
