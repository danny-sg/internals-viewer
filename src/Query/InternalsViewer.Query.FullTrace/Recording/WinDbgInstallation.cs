using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using InternalsViewer.Query.Server;

namespace InternalsViewer.Query.FullTrace.Recording;

/// <summary>
/// WinDbg installation location + debugger engine
/// </summary>
/// <remarks>
/// The Microsoft Store WinDbg records its install folder in the per-user package repository in the registry, which any process can read.
/// Its engine cannot be loaded from there, as the package folder refuses to map DLLs into processes outside the package, so
/// <see cref="PrepareEngine"/> copies the engine into a cache first. The engine has to be the one the debugger runs, since a session
/// refuses a client built from a different engine.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed record WinDbgInstallation(string Executable, string EngineDirectory, string Name, bool IsPackaged)
{
    private const string PackagesKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    private const string PackagePrefix = "Microsoft.WinDbg_";

    private const string PackageSuffix = "_8wekyb3d8bbwe";

    /// <summary>
    /// Finds the Store WinDbg, or failing that the Debugging Tools for Windows
    /// </summary>
    public static WinDbgInstallation? Locate(ILogger logger) => LocatePackage(logger) ?? LocateDebuggingTools(logger);

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

    public static WinDbgInstallation Resolve(string path, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            logger.LogInformation("Using the WinDbg Path from Settings: {Path}", path);

            if (!File.Exists(path))
            {
                logger.LogWarning("WinDbg was not found at {Path}", path);

                throw new WinDbgNotFoundException($"WinDbg was not found at {path}. Check the WinDbg Path in Settings");
            }

            var installation = FromExecutable(path);

            logger.LogInformation("Using WinDbg at {Executable} with the engine in {EngineDirectory}",
                                  installation.Executable,
                                  installation.EngineDirectory);

            return installation;
        }

        logger.LogInformation("The WinDbg Path is not set in Settings. Looking for an installation");

        var located = Locate(logger);

        if (located is null)
        {
            logger.LogWarning("WinDbg was not found. Install it, or set the WinDbg Path in Settings");

            throw new WinDbgNotFoundException("WinDbg was not found.");
        }

        return located;
    }

    public string? PrepareTimeTravel(string cacheRoot)
    {
        var source = new DirectoryInfo(Path.Combine(EngineDirectory, "ttd"));

        if (!File.Exists(Path.Combine(source.FullName, "TTD.exe")))
        {
            return null;
        }

        if (!IsPackaged)
        {
            return source.FullName;
        }

        var target = Path.Combine(cacheRoot, Name, "ttd");

        Directory.CreateDirectory(target);

        ServiceAccess.Grant(target, FileSystemRights.ReadAndExecute);

        foreach (var file in source.GetFiles())
        {
            var copy = new FileInfo(Path.Combine(target, file.Name));

            if (!copy.Exists || copy.LastWriteTimeUtc != file.LastWriteTimeUtc || copy.Length != file.Length)
            {
                file.CopyTo(copy.FullName, overwrite: true);
            }
        }

        return target;
    }

    private static WinDbgInstallation? LocatePackage(ILogger logger)
    {
        logger.LogInformation("Looking for the Microsoft Store WinDbg under HKEY_CURRENT_USER\\{Key}", PackagesKey);

        using var packages = Registry.CurrentUser.OpenSubKey(PackagesKey);

        if (packages is null)
        {
            logger.LogInformation("The package repository key was not found");

            return null;
        }

        var candidates = packages.GetSubKeyNames()
                                 .Where(n => n.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)
                                             && n.EndsWith(PackageSuffix, StringComparison.OrdinalIgnoreCase))
                                 .Select(n => (Name: n, Version: PackageVersion(n)))
                                 .OrderByDescending(p => p.Version)
                                 .ToList();

        if (candidates.Count == 0)
        {
            logger.LogInformation("No {Prefix}*{Suffix} package is registered for this user", PackagePrefix, PackageSuffix);

            return null;
        }

        var newest = candidates[0];

        logger.LogInformation("Found {Count} WinDbg package(s), newest {Name}", candidates.Count, newest.Name);

        using var package = packages.OpenSubKey(newest.Name);

        if (package?.GetValue("PackageRootFolder") is not string root || root.Length == 0)
        {
            logger.LogInformation("Package {Name} has no PackageRootFolder value", newest.Name);

            return null;
        }

        var engineDirectory = Path.Combine(root, "amd64");

        if (!File.Exists(Path.Combine(engineDirectory, "dbgeng.dll")))
        {
            logger.LogInformation("dbgeng.dll was not found at {Directory}", engineDirectory);

            return null;
        }

        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "Microsoft",
                                 "WindowsApps",
                                 "WinDbgX.exe");

        logger.LogInformation("Checking for the app execution alias at {Alias}", alias);

        var executable = File.Exists(alias) ? alias : Path.Combine(root, "DbgX.Shell.exe");

        logger.LogInformation("Using the Microsoft Store WinDbg {Name} at {Executable}", newest.Name, executable);

        return new WinDbgInstallation(executable, engineDirectory, newest.Name, IsPackaged: true);
    }

    private static WinDbgInstallation? LocateDebuggingTools(ILogger logger)
    {
        foreach (var programFiles in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var directory = Path.Combine(Environment.GetFolderPath(programFiles), "Windows Kits", "10", "Debuggers", "x64");

            logger.LogInformation("Checking for the Debugging Tools for Windows at {Directory}", directory);

            var executable = Path.Combine(directory, "windbg.exe");

            if (File.Exists(executable) && File.Exists(Path.Combine(directory, "dbgeng.dll")))
            {
                logger.LogInformation("Using the Debugging Tools for Windows at {Executable}", executable);

                return new WinDbgInstallation(executable, directory, "DebuggingTools", IsPackaged: false);
            }
        }

        logger.LogInformation("The Debugging Tools for Windows were not found");

        return null;
    }

    private static Version PackageVersion(string packageName)
    {
        var parts = packageName.Split('_');

        return parts.Length > 1 && Version.TryParse(parts[1], out var version) ? version : new Version(0, 0);
    }
}
