using InternalsViewer.Query.FullTrace.Recording;
using InternalsViewer.Query.FullTrace.Tests.Memory.MemoryObject;

namespace InternalsViewer.Query.FullTrace.Tests.Recording;

public class WinDbgInstallationTests
{
    [Fact]
    public void FromExecutable_Uses_The_Engine_Beside_A_Classic_WinDbg()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(directory.FullName, "dbgeng.dll"), []);

            var installation = WinDbgInstallation.FromExecutable(Path.Combine(directory.FullName, "windbg.exe"));

            Assert.Equal(directory.FullName, installation.EngineDirectory);
            Assert.False(installation.IsPackaged);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasTimeTravel_Is_True_With_TTD_In_The_Engine_Directory()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(directory.FullName, "dbgeng.dll"), []);

            Directory.CreateDirectory(Path.Combine(directory.FullName, "ttd"));

            File.WriteAllBytes(Path.Combine(directory.FullName, "ttd", "TTD.exe"), []);

            var installation = WinDbgInstallation.FromExecutable(Path.Combine(directory.FullName, "windbg.exe"));

            Assert.True(installation.HasTimeTravel);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasTimeTravel_Is_False_Without_TTD_In_The_Engine_Directory()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(directory.FullName, "dbgeng.dll"), []);

            var installation = WinDbgInstallation.FromExecutable(Path.Combine(directory.FullName, "windbg.exe"));

            Assert.False(installation.HasTimeTravel);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void FromExecutable_Treats_A_Store_Package_As_Packaged()
    {
        const string root = @"C:\Program Files\WindowsApps\Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe";

        var installation = WinDbgInstallation.FromExecutable(Path.Combine(root, "DbgX.Shell.exe"));

        Assert.True(installation.IsPackaged);
        Assert.Equal(@"C:\Program Files\WindowsApps\Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe\amd64", installation.EngineDirectory);
        Assert.Equal("Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe", installation.Name);
    }
}
