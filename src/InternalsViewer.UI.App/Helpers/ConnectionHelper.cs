using System;
using InternalsViewer.UI.App.Models.Connections;
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

    public static bool ProtectStoredPassword(RecentConnection recent)
    {
        if (!recent.IsServer)
        {
            return false;
        }

        SqlConnectionStringBuilder connectionStringBuilder;

        try
        {
            connectionStringBuilder = new SqlConnectionStringBuilder(recent.Value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (string.IsNullOrEmpty(connectionStringBuilder.Password))
        {
            return false;
        }

        recent.ProtectedPassword = PasswordProtection.Protect(connectionStringBuilder.Password);

        recent.IsPasswordRequired = true;

        connectionStringBuilder.Remove("Password");

        recent.Value = connectionStringBuilder.ConnectionString;

        return true;
    }
}
