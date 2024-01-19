using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using InternalsViewer.UI.App.vNext.Models.Connections;
using System.ComponentModel.DataAnnotations;
using InternalsViewer.UI.App.vNext.Services;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Messages;

namespace InternalsViewer.UI.App.vNext.ViewModels.Connections;

public partial class ServerConnectionViewModel(SettingsService settingsService) : ObservableValidator
{
    public SettingsService SettingsService { get; } = settingsService;

    private readonly SqlConnectionStringBuilder builder = new() { TrustServerCertificate = true };

    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    private string instanceName = "localhost";

    [Required]
    [ObservableProperty]
    private SqlAuthenticationMethod authenticationType = SqlAuthenticationMethod.ActiveDirectoryIntegrated;

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
    private string connectButtonText = "Connect";

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

    partial void OnAuthenticationTypeChanged(SqlAuthenticationMethod value)
    {
        builder.Authentication = value;

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

    public string GetConnectionString()
    {
        RefreshConnectionString();

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
            if ((!string.IsNullOrEmpty(Database) && !results.Contains(Database)))
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

    [RelayCommand]
    public async Task Connect()
    {
        var settings = GetSettings();
        var connectionString = GetConnectionString();

        IsBusy = true;
        ConnectButtonText = "Connecting...";

        var message = new ConnectServerMessage(connectionString, settings);

        WeakReferenceMessenger.Default.Send(message);

        await message.Response;

        ConnectButtonText = "Connect";

        IsBusy = false;
    }

    private void ClearDatabaseList()
    {
        Databases.Clear();

        if (!string.IsNullOrEmpty(Database))
        {
            Databases.Add(Database);
        }
    }

    public async Task InitializeAsync()
    {
        var settings = await SettingsService.ReadSettingAsync<ServerConnectionSettings>("CurrentServerConnection");  

        if (settings != null)
        {
            InstanceName = settings.InstanceName;
            AuthenticationType = (SqlAuthenticationMethod)settings.AuthenticationType;
            Database = settings.DatabaseName;
            UserId = settings.UserId;

            RefreshConnectionString();
        }
    }

    private void RefreshConnectionString()
    {
        builder.InitialCatalog = Database;
        builder.DataSource = InstanceName;
        
        builder.Authentication = AuthenticationType;

        if (!string.IsNullOrEmpty(UserId))
        {
            builder.UserID = UserId;
        }

        if (!string.IsNullOrEmpty(Password))
        {
            builder.Password = Password;
        }
    }
}
