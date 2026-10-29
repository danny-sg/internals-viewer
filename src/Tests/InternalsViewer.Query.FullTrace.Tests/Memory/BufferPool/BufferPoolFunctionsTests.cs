using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class BufferPoolFunctionsTests
{
    [Fact]
    public void Gets_And_Page_Read_Completions_Are_Collected()
    {
        var collector = new BufferPoolFunctions.Collector();

        collector.VisitFunction(0x10, "BPool::Get");

        collector.VisitFunction(0x20, "BPool::GetFromDisk");

        collector.VisitFunction(0x30, "FCB::PostRead");

        collector.VisitFunction(0x40, "BPool::ReadPageCompletion");

        var functions = collector.Build();

        Assert.Equal([0x10ul], functions.Gets);
        Assert.Equal([0x30ul], functions.PostReads);
    }
}
