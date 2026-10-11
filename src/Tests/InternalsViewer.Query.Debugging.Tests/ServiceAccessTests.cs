using System.Security.AccessControl;
using System.Security.Principal;

namespace InternalsViewer.Query.Debugging.Tests;

public class ServiceAccessTests
{
    private static SecurityIdentifier Service { get; } = new("S-1-5-80-3880718306-3832830129-1677859214-2598158968-1052248003");

    [Fact]
    public void PrivateSecurity_Grants_Only_System_Administrators_The_User_And_The_Service()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var security = ServiceAccess.PrivateSecurity(identity.User!, [Service]);

        var rules = Rules(security);

        Assert.True(security.AreAccessRulesProtected);

        Assert.Equal(4, rules.Count);

        Assert.Equal(FileSystemRights.FullControl, RightsOf(rules, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)));

        Assert.Equal(FileSystemRights.FullControl, RightsOf(rules, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)));

        Assert.Equal(FileSystemRights.FullControl, RightsOf(rules, identity.User!));

        Assert.Equal(FileSystemRights.Modify | FileSystemRights.Synchronize, RightsOf(rules, Service));
    }

    [Fact]
    public void CreatePrivate_Creates_A_Folder_That_Does_Not_Inherit_From_Its_Parent()
    {
        var parent = Directory.CreateTempSubdirectory();

        try
        {
            var path = ServiceAccess.CreatePrivate(Path.Combine(parent.FullName, "QueryReplay_0123456789abcdef0123456789abcdef"), Service);

            var security = new DirectoryInfo(path).GetAccessControl();

            var rules = Rules(security);

            Assert.True(security.AreAccessRulesProtected);

            Assert.DoesNotContain(rules, r => r.IsInherited);

            Assert.DoesNotContain(rules, r => r.IdentityReference == new SecurityIdentifier(WellKnownSidType.WorldSid, null));

            Assert.DoesNotContain(rules, r => r.IdentityReference == new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));

            Assert.Contains(rules, r => r.IdentityReference == Service);
        }
        finally
        {
            parent.Delete(recursive: true);
        }
    }

    [Fact]
    public void CreatePrivate_Falls_Back_To_All_Services_Without_A_Service()
    {
        var parent = Directory.CreateTempSubdirectory();

        try
        {
            var path = ServiceAccess.CreatePrivate(Path.Combine(parent.FullName, "Trace"), null);

            var rules = Rules(new DirectoryInfo(path).GetAccessControl());

            Assert.Contains(rules, r => r.IdentityReference == new SecurityIdentifier("S-1-5-80-0"));
        }
        finally
        {
            parent.Delete(recursive: true);
        }
    }

    [Fact]
    public void MakePrivate_Replaces_Inherited_Access_On_A_Folder_And_Its_Files_Once()
    {
        var parent = Directory.CreateTempSubdirectory();

        try
        {
            var directory = Directory.CreateDirectory(Path.Combine(parent.FullName, "Traces"));

            var file = Path.Combine(directory.FullName, "QueryReplay_0123456789abcdef0123456789abcdef.xel");

            File.WriteAllBytes(file, []);

            Assert.True(ServiceAccess.MakePrivate(directory.FullName, [Service]));

            var security = directory.GetAccessControl();

            Assert.True(security.AreAccessRulesProtected);

            Assert.DoesNotContain(Rules(security), r => r.IsInherited);

            var fileRules = new FileInfo(file).GetAccessControl()
                                              .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                                              .OfType<FileSystemAccessRule>()
                                              .ToList();

            Assert.Contains(fileRules, r => r.IdentityReference == Service && r.IsInherited);

            Assert.False(ServiceAccess.MakePrivate(directory.FullName, [Service]));
        }
        finally
        {
            parent.Delete(recursive: true);
        }
    }

    [Fact]
    public void ServiceSid_Is_Null_For_A_Service_That_Does_Not_Exist()
    {
        Assert.Null(ServiceAccess.ServiceSid("InternalsViewerNoSuchService"));
    }

    private static List<FileSystemAccessRule> Rules(DirectorySecurity security)
        => security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                   .OfType<FileSystemAccessRule>()
                   .ToList();

    private static FileSystemRights RightsOf(List<FileSystemAccessRule> rules, SecurityIdentifier identity)
        => Assert.Single(rules, r => r.IdentityReference == identity).FileSystemRights;
}
