using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.Services.XEvents;

/// <summary>
/// Manages the directory XEvent (.xel) trace files are written to when the SQL Server log directory is not used
/// </summary>
/// <remarks>
/// SQL Server (the service, not this app) writes the trace file, so a custom directory must be writable by the SQL Server service account.
///
/// Rather than resolve one instance's account from a connection, this grants every local SQL Server engine service's per-service SID
/// (<c>NT SERVICE\MSSQLSERVER</c> / <c>NT SERVICE\MSSQL$INSTANCE</c>) — that SID is in the service token whatever the service actually
/// runs as, and covers whichever instance the user connects to.
/// </remarks>
public sealed class TraceDirectoryService(ILogger<TraceDirectoryService> logger)
{
    private ILogger<TraceDirectoryService> Logger { get; } = logger;

    public string DefaultDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                                           "InternalsViewer", 
                                                           "Traces");

    /// <summary>
    /// Creates the directory (if needed) and grants each local SQL Server engine service Modify access to it
    /// </summary>
    public GrantResult GrantPermissions(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new GrantResult(false, "No directory specified.");
        }

        try
        {
            Directory.CreateDirectory(directory);

            var accounts = FindSqlServiceAccounts();

            if (accounts.Count == 0)
            {
                return new GrantResult(false, "No local SQL Server service found to grant access to.");
            }

            var info = new DirectoryInfo(directory);

            var security = info.GetAccessControl();

            var granted = false;

            foreach (var account in accounts)
            {
                if (HasModify(security, account))
                {
                    continue;
                }

                security.AddAccessRule(new FileSystemAccessRule(new NTAccount(account),
                                                                FileSystemRights.Modify,
                                                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                                PropagationFlags.None,
                                                                AccessControlType.Allow));

                granted = true;
            }

            if (granted)
            {
                info.SetAccessControl(security);
            }

            return new GrantResult(true,
                                   granted
                                       ? $"Granted write access to {string.Join(", ", accounts)}."
                                       : "Write access already granted.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to grant trace directory permissions on {Directory}", directory);

            return new GrantResult(false, $"Could not grant access: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether every local SQL Server service already has 'Modify' access to the directory
    /// </summary>
    public bool HasPermissions(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            var accounts = FindSqlServiceAccounts();

            if (accounts.Count == 0)
            {
                return false;
            }

            var security = new DirectoryInfo(directory).GetAccessControl();

            return accounts.All(a => HasModify(security, a));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to check trace directory permissions on {Directory}", directory);

            return false;
        }
    }

    private static bool HasModify(DirectorySecurity security, string account) =>
        security.GetAccessRules(true, true, typeof(NTAccount))
                .Cast<FileSystemAccessRule>()
                .Any(rule => rule.AccessControlType == AccessControlType.Allow
                             && rule.IdentityReference.Value.Equals(account, StringComparison.OrdinalIgnoreCase)
                             && (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify);

    private static List<string> FindSqlServiceAccounts()
    {
        var services = ServiceController.GetServices();

        try
        {
            return services
                   .Where(s => s.ServiceName.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
                               || s.ServiceName.StartsWith("MSSQL$", StringComparison.OrdinalIgnoreCase))
                   .Select(s => $"NT SERVICE\\{s.ServiceName}")
                   .ToList();
        }
        finally
        {
            foreach (var service in services)
            {
                service.Dispose();
            }
        }
    }
}

/// <summary>
/// Outcome of a permission grant: whether it succeeded and a message to surface to the user
/// </summary>
public readonly record struct GrantResult(bool Success, string Message);
