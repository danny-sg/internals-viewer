using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class BufferPoolFunctionLookupTests
{
    [Fact]
    public void Gets_And_Page_Read_Completions_Are_Collected()
    {
        var collector = BufferPoolFunctionLookup.CreateCollector();

        collector.VisitFunction(0x10, "BPool::Get");

        collector.VisitFunction(0x20, "BPool::GetFromDisk");

        collector.VisitFunction(0x30, "FCB::PostRead");

        collector.VisitFunction(0x40, "BPool::ReadPageCompletion");

        var functions = collector.Build().OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, BufferPoolFunctionType)[] expected = [(0x10, BufferPoolFunctionType.Get), (0x30, BufferPoolFunctionType.PostRead)];

        Assert.Equal(expected, functions);
    }
}
