using Microsoft.Data.SqlClient;

namespace InternalsViewer.UI.App.Helpers;

internal static class ConnectionHelper
{
    public static string SetPassword(string connectionString, string result)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString); 

        connectionStringBuilder.Password = result;

        return connectionStringBuilder.ToString();
    }
}
