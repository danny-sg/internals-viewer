using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Tests.Memory;

[Trait("Category", "Unit")]
public class MemoryObjectNamesTests
{
    [Theory]
    [InlineData("MEMOBJ_EXECUTE", "Internal Execution Plan (MXC)")]
    [InlineData("MEMOBJ_COMPILE_ADHOC", "Compiled Plan (CP)")]
    [InlineData("MEMOBJ_QUERYEXECCNTXTFORSE", "Storage Engine Query Context")]
    [InlineData("MEMOBJ_XSTMT", "Execution Statement (XStmt)")]
    [InlineData("MEMOBJ_SOSWORKER", "MEMOBJ_SOSWORKER")]
    public void Plan_Cache_Memory_Objects_Are_Named_And_Others_Keep_Their_Type(string type, string expected)
    {
        Assert.Equal(expected, MemoryObjectNames.GetName(type));
    }
}
