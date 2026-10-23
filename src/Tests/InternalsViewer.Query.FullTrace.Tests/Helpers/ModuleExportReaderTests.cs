using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Tests.Helpers;

[Trait("Category", "Unit")]
public class ModuleExportReaderTests
{
    [Fact]
    public void A_System_Module_Lists_Its_Heap_Exports()
    {
        var exports = ModuleExportReader.Read(Path.Combine(Environment.SystemDirectory, "ntdll.dll"));

        Assert.Contains(exports, e => e.Name == "RtlAllocateHeap" && e.Rva > 0);
    }

    [Fact]
    public void A_Forwarded_Export_Is_Left_Out()
    {
        var exports = ModuleExportReader.Read(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));

        Assert.DoesNotContain(exports, e => e.Name == "HeapAlloc");
        Assert.Contains(exports, e => e.Name == "GetProcessHeap" || e.Name == "CreateFileW");
    }

    [Fact]
    public void A_Missing_File_Has_No_Exports() => Assert.Empty(ModuleExportReader.Read(@"Z:\missing\module.dll"));
}
