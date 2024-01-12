using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using InternalsViewer.UI.App.vNext.Models.Connections;
using System.ComponentModel.DataAnnotations;

namespace InternalsViewer.UI.App.vNext.ViewModels.Connections;

public partial class ServerConnectionViewModel : ObservableValidator
{
    public void SetInitialSettings(ServerConnectionSettings serverConnectionSettings)
    {
        InstanceName = serverConnectionSettings.InstanceName;
        AuthenticationType = (SqlAuthenticationMethod?)serverConnectionSettings.AuthenticationType;
        Database = serverConnectionSettings.DatabaseName;
        UserId = serverConnectionSettings.UserId;

        if(Databases.Count == 0)
        {
            Databases.Add(Database);
        }
    }

    private readonly SqlConnectionStringBuilder builder = new() { TrustServerCertificate = true };

    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    private string instanceName = string.Empty;

    [Required]
    [ObservableProperty]
    private SqlAuthenticationMethod? authenticationType;

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    private string database = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> databases = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isUserIdEnabled;

    [ObservableProperty]
    private bool isPasswordEnabled;

    public List<KeyValuePair<SqlAuthenticationMethod, string>> AuthenticationTypes => new()
    {
        new (SqlAuthenticationMethod.ActiveDirectoryIntegrated, "Active Directory Integrated"),
        new (SqlAuthenticationMethod.SqlPassword, "SQL Password"),
        new (SqlAuthenticationMethod.ActiveDirectoryDefault, "Active Directory Default"),
        new (SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, "Active Directory Device Code Flow"),
        new (SqlAuthenticationMethod.ActiveDirectoryInteractive, "Active Directory Interactive"),
        new (SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, "Active Directory Managed Identity"),
        new (SqlAuthenticationMethod.ActiveDirectoryMSI, "Active Directory MSI"),
        new (SqlAuthenticationMethod.ActiveDirectoryPassword, "Active Directory Password"),
        new (SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, "Active Directory Service Principal"),
    };

    partial void OnUserIdChanging(string value)
    {
        builder.DataSource = value;
    }

    partial void OnAuthenticationTypeChanged(SqlAuthenticationMethod? value)
    {
        if (value == null)
        {
            builder.Remove(nameof(SqlAuthenticationMethod));

            return;
        }

        builder.Authentication = value.Value;

        switch (builder)
        {
            case { Authentication: SqlAuthenticationMethod.SqlPassword }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryServicePrincipal }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryPassword }:
                IsUserIdEnabled = true;
                IsPasswordEnabled = true;
                break;
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryIntegrated }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryInteractive }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryManagedIdentity }:
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryMSI }:
                IsUserIdEnabled = true;
                IsPasswordEnabled = false;

                builder.Remove(nameof(Password));

                break;
            case { Authentication: SqlAuthenticationMethod.ActiveDirectoryDefault }:
            case { Authentication: SqlAuthenticationMethod.NotSpecified }:

                IsUserIdEnabled = false;
                IsPasswordEnabled = false;

                builder.Remove(nameof(UserId));
                builder.Remove(nameof(Password));
                break;
        }
    }

    partial void OnUserIdChanged(string value)
    {
        builder.UserID = value;
    }

    partial void OnPasswordChanged(string value)
    {
        builder.Password = value;
    }

    partial void OnDatabaseChanged(string value)
    {
        builder.InitialCatalog = value;
    }

    public string GetConnectionString()
    {
        return builder.ConnectionString;
    }

    public ServerConnectionSettings GetSettings() =>
        new()
        {
            InstanceName = instanceName,
            AuthenticationType = (int?)authenticationType,
            DatabaseName = database,
            UserId = userId
        };

    public async Task RefreshDatabases()
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return;
        }

        isBusy = true;

        try
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(builder.ConnectionString);

            connectionStringBuilder.InitialCatalog = string.Empty;

            connectionStringBuilder.TrustServerCertificate = true;

            var results = new List<string>();

            await using (var connection = new SqlConnection(connectionStringBuilder.ConnectionString))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();

                command.CommandText = "SELECT [name] FROM sys.databases ORDER BY [name]";

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(reader.GetString(0));
                }
            }
            if ((!string.IsNullOrEmpty(Database) || !results.Contains(Database)))
            {
                results.Insert(0, Database);
            }

            Databases = new ObservableCollection<string>(results);
        }
        catch (Exception)
        {
            ClearDatabaseList();
        }
        finally
        {
            isBusy = false;
        }
    }

    private void ClearDatabaseList()
    {
        Databases.Clear();

        if (!string.IsNullOrEmpty(Database))
        {
            Databases.Add(Database);
        }
    }
}
