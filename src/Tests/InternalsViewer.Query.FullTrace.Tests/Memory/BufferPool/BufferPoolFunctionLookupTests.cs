using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class BufferPoolFunctionLookupTests
{
    [Fact]
    public void Gets_And_Page_Read_Completions_Are_Found_By_Name()
    {
        string[] names = ["BPool::Get", "BPool::GetFromDisk", "FCB::PostRead", "BPool::ReadPageCompletion"];

        Assert.Equal([BufferPoolFunctionType.Get, null, BufferPoolFunctionType.PostRead, null],
                     names.Select(n => BufferPoolFunctionLookup.GetFunctionType(n)));
    }
}
