using InternalsViewer.Query.Debugging;

namespace InternalsViewer.Query.Debugging.Tests;

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
            var cache = Path.Combine(directory.FullName, "cache");

            Assert.Equal(Path.Combine(directory.FullName, "dbgeng.dll"), installation.PrepareEngine(cache));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void FromExecutable_Treats_A_Store_Package_As_Needing_Its_Engine_Copied()
    {
        const string root = @"C:\Program Files\WindowsApps\Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe";

        var installation = WinDbgInstallation.FromExecutable(Path.Combine(root, "DbgX.Shell.exe"));

        Assert.True(installation.IsPackaged);
        Assert.Equal(@"C:\Program Files\WindowsApps\Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe\amd64", installation.EngineDirectory);
        Assert.Equal("Microsoft.WinDbg_1.0_x64__8wekyb3d8bbwe", installation.Name);
    }

    [Fact]
    public void PrepareEngine_Copies_The_Engine_Once_And_Again_When_It_Changes()
    {
        var root = Directory.CreateTempSubdirectory();

        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root.FullName, "amd64"));

            File.WriteAllText(Path.Combine(source.FullName, "dbgeng.dll"), "v1");
            File.WriteAllText(Path.Combine(source.FullName, "dbghelp.dll"), "v1");

            var installation = new WinDbgInstallation("x.exe", source.FullName, "Pkg", IsPackaged: true);

            var cache = Path.Combine(root.FullName, "cache");

            var engine = installation.PrepareEngine(cache);

            Assert.Equal(Path.Combine(cache, "Pkg", "dbgeng.dll"), engine);
            Assert.Equal("v1", File.ReadAllText(engine));
            Assert.True(File.Exists(Path.Combine(cache, "Pkg", "dbghelp.dll")));

            File.WriteAllText(Path.Combine(source.FullName, "dbgeng.dll"), "v2!");

            installation.PrepareEngine(cache);

            Assert.Equal("v2!", File.ReadAllText(engine));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
