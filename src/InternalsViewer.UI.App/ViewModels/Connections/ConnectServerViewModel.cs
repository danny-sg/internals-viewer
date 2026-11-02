using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public sealed class ConnectServerViewModelFactory(ILogger<ConnectServerViewModel> logger,
                                                   SettingsService settingsService)
{
    private ILogger<ConnectServerViewModel> Logger { get; } = logger;

    private SettingsService SettingsService { get; } = settingsService;

    public ConnectServerViewModel Create() => new(Logger, SettingsService);
}

public partial class ConnectServerViewModel(ILogger<ConnectServerViewModel> logger,
                                            SettingsService settingsService)
    : ObservableValidator
{
    private readonly SqlConnectionStringBuilder _builder = new() { TrustServerCertificate = true };

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
    [NotifyPropertyChangedFor(nameof(IsDatabaseListErrorVisible))]
    private string _databaseListError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshDatabasesCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDatabaseListRefreshing;

    [ObservableProperty]
    private string _connectButtonText = "Connect";

    public bool IsUserIdEnabled => AuthenticationType != (int)SqlAuthenticationMethod.ActiveDirectoryIntegrated;

#pragma warning disable CS0618 // Type or member is obsolete
    public bool IsPasswordEnabled 
        => AuthenticationType is (int)SqlAuthenticationMethod.SqlPassword or (int)SqlAuthenticationMethod.ActiveDirectoryPassword;

    public List<AuthenticationTypeOption> AuthenticationTypes =>
    [
        new((int) SqlAuthenticationMethod.ActiveDirectoryIntegrated, "Windows Authentication"),
        new((int) SqlAuthenticationMethod.SqlPassword, "SQL Server Authentication"),
        new((int) SqlAuthenticationMethod.ActiveDirectoryPassword, "Active Directory Password")
    ];
#pragma warning restore CS0618 // Type or member is obsolete

    public bool IsDatabaseListErrorVisible => !string.IsNullOrEmpty(DatabaseListError);

    private ILogger<ConnectServerViewModel> Logger { get; } = logger;

    private SettingsService SettingsService { get; } = settingsService;

    private CancellationTokenSource? RefreshCancellation { get; set; }

    public bool CanConnect() => !HasErrors && !IsBusy;    

    public bool CanRefreshDatabases() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefreshDatabases))]
    public async Task RefreshDatabases()
    {
        if (string.IsNullOrWhiteSpace(InstanceName))
        {
            return;
        }

        RefreshCancellation?.Cancel();

        using var cancellation = new CancellationTokenSource();

        RefreshCancellation = cancellation;

        IsBusy = true;
        IsDatabaseListRefreshing = true;
        DatabaseListError = string.Empty;

        try
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(GetConnectionString());

            connectionStringBuilder.InitialCatalog = string.Empty;

            connectionStringBuilder.TrustServerCertificate = true;

            var results = new List<string>();

            await using (var connection = new SqlConnection(connectionStringBuilder.ConnectionString))
            {
                await connection.OpenAsync(cancellation.Token);

                await using var command = connection.CreateCommand();

                command.CommandText = "SELECT [name] FROM sys.databases ORDER BY [name]";

                await using var reader = await command.ExecuteReaderAsync(cancellation.Token);

                while (await reader.ReadAsync(cancellation.Token))
                {
                    results.Add(reader.GetString(0));
                }
            }

            if (!cancellation.IsCancellationRequested)
            {
                SetDatabaseList(results);
            }
        }
        catch (Exception ex)
        {
            if (!cancellation.IsCancellationRequested)
            {
                Logger.LogError(ex, "Failed to refresh databases for instance: {InstanceName}", InstanceName);

                DatabaseListError = ex.Message;

                SetDatabaseList([]);
            }
        }
        finally
        {
            if (!cancellation.IsCancellationRequested)
            {
                RefreshCancellation = null;

                IsBusy = false;
                IsDatabaseListRefreshing = false;
            }
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

    /// <summary>
    /// Gets the connection string from the current settings
    /// </summary>
    internal string GetConnectionString()
    {
        RefreshConnectionString();

        return _builder.ConnectionString;
    }

    internal void SetDatabaseList(List<string> names)
    {
        var database = Database;

        if (!string.IsNullOrEmpty(database) && !names.Contains(database))
        {
            names.Insert(0, database);
        }

        Databases = new ObservableCollection<string>(names);

        Database = database;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ValidateAllProperties();

        base.OnPropertyChanged(e);
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
            Value = GetSafeConnectionString(),
            IsPasswordRequired = IsPasswordEnabled,
            ProtectedPassword = IsPasswordEnabled && !string.IsNullOrEmpty(Password) ? PasswordProtection.Protect(Password) : null
        };

        return recent;
    }

    private void RefreshConnectionString()
    {
        _builder.InitialCatalog = Database ?? "master";
        _builder.DataSource = InstanceName;

        _builder.Authentication = (SqlAuthenticationMethod)AuthenticationType;

        _builder.Remove("User ID");
        _builder.Remove(nameof(Password));

        if (IsUserIdEnabled && !string.IsNullOrEmpty(UserId))
        {
            _builder.UserID = UserId;
        }

        if (IsPasswordEnabled && !string.IsNullOrEmpty(Password))
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
