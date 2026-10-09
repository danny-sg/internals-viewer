using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ModuleExportsTests
{
    [Fact]
    public void A_System_Module_Lists_Its_Heap_Exports()
    {
        var exports = ModuleExports.Read(Path.Combine(Environment.SystemDirectory, "ntdll.dll"));

        Assert.Contains(exports, e => e.Name == "RtlAllocateHeap" && e.Rva > 0);
    }

    [Fact]
    public void A_Forwarded_Export_Is_Left_Out()
    {
        var exports = ModuleExports.Read(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));

        Assert.DoesNotContain(exports, e => e.Name == "HeapAlloc");
        Assert.Contains(exports, e => e.Name == "GetProcessHeap" || e.Name == "CreateFileW");
    }

    [Fact]
    public void A_Missing_File_Has_No_Exports() => Assert.Empty(ModuleExports.Read(@"Z:\missing\module.dll"));
}
