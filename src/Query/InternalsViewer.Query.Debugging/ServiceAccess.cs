using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace InternalsViewer.Query.Debugging;

[SupportedOSPlatform("windows")]
public static class ServiceAccess
{
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

        security.AddAccessRule(new FileSystemAccessRule(AllServices,
                                                        rights,
                                                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                        PropagationFlags.None,
                                                        AccessControlType.Allow));

        info.SetAccessControl(security);
    }
}
