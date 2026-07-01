using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.Services;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public sealed class ConnectServerViewModelFactory(SettingsService settingsService)
{
    private SettingsService SettingsService { get; } = settingsService;

    public ConnectServerViewModel Create() => new(SettingsService);
}

public partial class ConnectServerViewModel(SettingsService settingsService) : ObservableValidator
{
    private SettingsService SettingsService { get; } = settingsService;

    private readonly SqlConnectionStringBuilder _builder = new() { TrustServerCertificate = true };

    public bool CanConnect() => !HasErrors && !IsBusy;    

    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _instanceName = "localhost";

    [Required]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyPropertyChangedFor(nameof(IsPasswordEnabled))]
    [NotifyPropertyChangedFor(nameof(IsUserIdEnabled))]
    private int _authenticationType = (int)SqlAuthenticationMethod.ActiveDirectoryIntegrated;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _userId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _password = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _database = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _databases = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _connectButtonText = "Connect";

    public bool IsUserIdEnabled => AuthenticationType != (int)SqlAuthenticationMethod.ActiveDirectoryIntegrated;

    public bool IsPasswordEnabled => AuthenticationType == (int)SqlAuthenticationMethod.SqlPassword || AuthenticationType == (int)SqlAuthenticationMethod.ActiveDirectoryPassword;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ValidateAllProperties();

        base.OnPropertyChanged(e);
    }


#pragma warning disable CS0618 // Type or member is obsolete
    public List<AuthenticationTypeOption> AuthenticationTypes => new()
    {
        new ((int)SqlAuthenticationMethod.ActiveDirectoryIntegrated, "Windows Authentication"),
        new ((int)SqlAuthenticationMethod.SqlPassword, "SQL Server Authentication"),
        new ((int)SqlAuthenticationMethod.ActiveDirectoryPassword, "Active Directory Password")
    };
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Gets the connection string from the current settings
    /// </summary>
    private string GetConnectionString()
    {
        RefreshConnectionString();

        return _builder.ConnectionString;
    }

    /// <summary>
    /// Gets the connection string without the password
    /// </summary>
    private string GetSafeConnectionString()
    {
        RefreshConnectionString();

        _builder.Remove(nameof(Password));

        return _builder.ConnectionString;
    }

    private ServerConnectionSettings GetSettings() =>
        new()
        {
            InstanceName = InstanceName,
            AuthenticationType = AuthenticationType,
            DatabaseName = Database ?? string.Empty,
            UserId = UserId
        };

    public async Task RefreshDatabases()
    {
        if (string.IsNullOrWhiteSpace(InstanceName))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(GetConnectionString());

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
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task Connect()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            return;
        }

        var recent = GetRecent();

        var connectionString = GetConnectionString();

        IsBusy = true;

        ConnectButtonText = "Connecting...";

        if (await CheckConnection(connectionString))
        {
            var message = new ConnectServerMessage(connectionString, recent) { IsPasswordRequired = false };

            await WeakReferenceMessenger.Default.Send(message);

            await message.Response;

            await SettingsService.SaveSettingAsync("CurrentServerConnection", GetSettings());
        }

        ConnectButtonText = "Connect";

        IsBusy = false;
    }

    private static async Task<bool> CheckConnection(string connectionString)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync();

            return true;
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));

            return false;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private RecentConnection GetRecent()
    {
        var recent = new RecentConnection
        {
            Name = $"{InstanceName}.{Database}",
            ConnectionType = "Server",
            Value = GetConnectionString(),
            IsPasswordRequired = AuthenticationType == (int)SqlAuthenticationMethod.SqlPassword
        };

        return recent;
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
            AuthenticationType = settings.AuthenticationType;
            Database = settings.DatabaseName;
            UserId = settings.UserId;

            if (Databases.Count == 0)
            {
                Databases.Add(Database);
            }

            RefreshConnectionString();
        }

        ValidateAllProperties();
    }

    private void RefreshConnectionString()
    {
        _builder.InitialCatalog = Database ?? "master";
        _builder.DataSource = InstanceName;

        _builder.Authentication = (SqlAuthenticationMethod)AuthenticationType;

        if (!string.IsNullOrEmpty(UserId))
        {
            _builder.UserID = UserId;
        }

        if (!string.IsNullOrEmpty(Password))
        {
            _builder.Password = Password;
        }
    }
}

public sealed class AuthenticationTypeOption(int id, string name)
{
    public int Id { get; set; } = id;

    public string Name { get; set; } = name;
}
