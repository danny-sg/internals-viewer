using System.Runtime.Versioning;
using Microsoft.Win32;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// WinDbg installation location + debugger engine
/// </summary>
/// <remarks>
/// The Microsoft Store WinDbg records its install folder in the per-user package repository in the registry, which
/// any process can read. Its engine cannot be loaded from there, as the package folder refuses to map DLLs into
/// processes outside the package, so <see cref="PrepareEngine"/> copies the engine into a cache first. The engine
/// has to be the one the debugger runs, since a session refuses a client built from a different engine.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed record WinDbgInstallation(string Executable, string EngineDirectory, string Name, bool IsPackaged)
{
    private const string PackagesKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    private const string PackagePrefix = "Microsoft.WinDbg_";

    private const string PackageSuffix = "_8wekyb3d8bbwe";

    private static readonly string[] EngineFiles =
        ["dbgeng.dll", "dbghelp.dll", "dbgcore.dll", "dbgmodel.dll", "symsrv.dll", "srcsrv.dll"];

    /// <summary>
    /// Finds the Store WinDbg, or failing that the Debugging Tools for Windows
    /// </summary>
    public static WinDbgInstallation? Locate() => LocatePackage() ?? LocateDebuggingTools();

    /// <summary>
    /// Describes an installation from the executable the user pointed at
    /// </summary>
    public static WinDbgInstallation FromExecutable(string executable)
    {
        var directory = Path.GetDirectoryName(executable) ?? string.Empty;

        var packaged = directory.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase);

        var engineDirectory = File.Exists(Path.Combine(directory, "dbgeng.dll")) ? directory : Path.Combine(directory, "amd64");

        return new WinDbgInstallation(executable, engineDirectory, Path.GetFileName(directory), packaged);
    }

    /// <summary>
    /// Returns the path of a loadable <c>dbgeng.dll</c>
    /// </summary>
    public string PrepareEngine(string cacheRoot)
    {
        if (!IsPackaged)
        {
            return Path.Combine(EngineDirectory, "dbgeng.dll");
        }

        var target = Path.Combine(cacheRoot, Name);

        Directory.CreateDirectory(target);

        foreach (var file in EngineFiles)
        {
            var source = new FileInfo(Path.Combine(EngineDirectory, file));

            var copy = new FileInfo(Path.Combine(target, file));

            if (source.Exists && (!copy.Exists || copy.LastWriteTimeUtc != source.LastWriteTimeUtc || copy.Length != source.Length))
            {
                source.CopyTo(copy.FullName, overwrite: true);
            }
        }

        return Path.Combine(target, "dbgeng.dll");
    }

    private static WinDbgInstallation? LocatePackage()
    {
        using var packages = Registry.CurrentUser.OpenSubKey(PackagesKey);

        if (packages is null)
        {
            return null;
        }

        var newest = packages.GetSubKeyNames()
                             .Where(n => n.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)
                                         && n.EndsWith(PackageSuffix, StringComparison.OrdinalIgnoreCase))
                             .Select(n => (Name: n, Version: PackageVersion(n)))
                             .OrderByDescending(p => p.Version)
                             .FirstOrDefault();

        if (newest.Name is null)
        {
            return null;
        }

        using var package = packages.OpenSubKey(newest.Name);

        if (package?.GetValue("PackageRootFolder") is not string root || root.Length == 0)
        {
            return null;
        }

        var engineDirectory = Path.Combine(root, "amd64");

        if (!File.Exists(Path.Combine(engineDirectory, "dbgeng.dll")))
        {
            return null;
        }

        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "Microsoft",
                                 "WindowsApps",
                                 "WinDbgX.exe");

        var executable = File.Exists(alias) ? alias : Path.Combine(root, "DbgX.Shell.exe");

        return new WinDbgInstallation(executable, engineDirectory, newest.Name, IsPackaged: true);
    }

    private static WinDbgInstallation? LocateDebuggingTools()
    {
        foreach (var programFiles in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var directory = Path.Combine(Environment.GetFolderPath(programFiles), "Windows Kits", "10", "Debuggers", "x64");

            var executable = Path.Combine(directory, "windbg.exe");

            if (File.Exists(executable) && File.Exists(Path.Combine(directory, "dbgeng.dll")))
            {
                return new WinDbgInstallation(executable, directory, "DebuggingTools", IsPackaged: false);
            }
        }

        return null;
    }

    private static Version PackageVersion(string packageName)
    {
        var parts = packageName.Split('_');

        return parts.Length > 1 && Version.TryParse(parts[1], out var version) ? version : new Version(0, 0);
    }
}
