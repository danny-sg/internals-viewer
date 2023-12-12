using Microsoft.Extensions.Configuration;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Helpers;
internal class ConnectionStringHelper
{
    public static string GetConnectionString(string name)
    {
        var builder = new ConfigurationBuilder().AddUserSecrets<ConnectionStringHelper>();

        var configuration = builder.Build();

        return configuration.GetConnectionString(name);
    }
}
