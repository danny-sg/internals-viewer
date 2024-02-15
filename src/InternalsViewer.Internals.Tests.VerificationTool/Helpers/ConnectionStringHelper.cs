using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace InternalsViewer.Internals.Tests.VerificationTool.Helpers;

internal class ConnectionStringHelper
{
    public static string GetConnectionString(string databaseName)
    {
        var builder = new ConfigurationBuilder().AddUserSecrets<ConnectionStringHelper>();

        var configuration = builder.Build();

        var connectionString = configuration.GetConnectionString("Default");

        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
            CommandTimeout = 60
        };
        return connectionStringBuilder.ToString();
    }
}