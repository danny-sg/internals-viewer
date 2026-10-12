using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace InternalsViewer.Query.Server;

[SupportedOSPlatform("windows")]
public static class ServiceAccess
{
    private const InheritanceFlags Inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    private static SecurityIdentifier AllServices { get; } = new("S-1-5-80-0");

    public static void Grant(string directory, FileSystemRights rights)
    {
        var info = new DirectoryInfo(directory);

        var security = info.GetAccessControl();

        var granted = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                              .OfType<FileSystemAccessRule>()
                              .Any(r => r.IdentityReference == AllServices
                                        && r.AccessControlType == AccessControlType.Allow
                                        && (r.FileSystemRights & rights) == rights);

        if (granted)
        {
            return;
        }

        security.AddAccessRule(Allow(AllServices, rights));

        info.SetAccessControl(security);
    }

    public static string CreatePrivate(string directory, SecurityIdentifier? service)
    {
        using var identity = WindowsIdentity.GetCurrent();

        var info = new DirectoryInfo(directory);

        info.Create(PrivateSecurity(identity.User!, [service ?? AllServices]));

        return info.FullName;
    }

    public static bool MakePrivate(string directory, IReadOnlyList<SecurityIdentifier> services)
    {
        using var identity = WindowsIdentity.GetCurrent();

        var info = new DirectoryInfo(directory);

        var security = PrivateSecurity(identity.User!, services);

        var current = info.GetAccessControl();

        if (current.AreAccessRulesProtected && RulesOf(current).SetEquals(RulesOf(security)))
        {
            return false;
        }

        info.SetAccessControl(security);

        return true;
    }

    public static SecurityIdentifier? ServiceSid(string serviceName)
    {
        try
        {
            return (SecurityIdentifier)new NTAccount("NT SERVICE", serviceName).Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    internal static DirectorySecurity PrivateSecurity(SecurityIdentifier user, IReadOnlyList<SecurityIdentifier> services)
    {
        var security = new DirectorySecurity();

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        security.AddAccessRule(Allow(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl));

        security.AddAccessRule(Allow(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl));

        security.AddAccessRule(Allow(user, FileSystemRights.FullControl));

        foreach (var service in services)
        {
            security.AddAccessRule(Allow(service, FileSystemRights.Modify));
        }

        return security;
    }

    private static FileSystemAccessRule Allow(IdentityReference identity, FileSystemRights rights)
        => new(identity, rights, Inheritance, PropagationFlags.None, AccessControlType.Allow);

    private static HashSet<(IdentityReference, FileSystemRights, InheritanceFlags, AccessControlType)> RulesOf(DirectorySecurity security)
        => security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                   .OfType<FileSystemAccessRule>()
                   .Select(r => (r.IdentityReference, r.FileSystemRights, r.InheritanceFlags, r.AccessControlType))
                   .ToHashSet();
}
