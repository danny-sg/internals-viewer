using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
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
    public void An_Export_That_Jumps_To_Its_Body_Also_Names_The_Body()
    {
        var exports = ModuleExportReader.Read(Path.Combine(Environment.SystemDirectory, "vcruntime140.dll"));

        var memset = exports.Where(e => e.Name == "memset").Select(e => e.Rva).ToList();

        Assert.Equal(2, memset.Count);
        Assert.NotEqual(memset[0], memset[1]);
    }

    [Fact]
    public void A_Missing_File_Has_No_Exports() => Assert.Empty(ModuleExportReader.Read(@"Z:\missing\module.dll"));
}
