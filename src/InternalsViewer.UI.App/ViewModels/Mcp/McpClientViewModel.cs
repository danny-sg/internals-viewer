using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;

namespace InternalsViewer.UI.App.ViewModels.Mcp;

public sealed partial class McpClientViewModel(IMcpClient client, Func<McpHostSettings> getSettings) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegisterLabel))]
    [NotifyPropertyChangedFor(nameof(IsRegistered))]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    [NotifyCanExecuteChangedFor(nameof(UnregisterCommand))]
    private McpClientStatus _status = McpClientStatus.NotInstalled;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public string Name => Client.Name;

    public string Description => Client.Description;

    public Uri? HelpUrl => Client.HelpUrl is { } url ? new Uri(url) : null;

    public bool IsRegistered => Status is McpClientStatus.Registered or McpClientStatus.NeedsUpdate;

    public string RegisterLabel => Status == McpClientStatus.NeedsUpdate ? "Update" : "Register";

    private IMcpClient Client { get; } = client;

    private Func<McpHostSettings> GetSettings { get; } = getSettings;

    public async Task RefreshAsync()
    {
        try
        {
            var state = await Client.GetStateAsync(GetSettings(), CancellationToken.None);

            Status = state.Status;

            StatusText = state.Detail;
        }
        catch (Exception exception)
        {
            Status = McpClientStatus.NotRegistered;

            StatusText = exception.Message;
        }
    }

    public async Task UpdateIfRegisteredAsync()
    {
        if (IsRegistered)
        {
            await RegisterAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        try
        {
            await Client.RegisterAsync(GetSettings(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;

            return;
        }

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUnregister))]
    private async Task UnregisterAsync()
    {
        try
        {
            await Client.UnregisterAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;

            return;
        }

        await RefreshAsync();
    }

    private bool CanRegister() => Status is McpClientStatus.NotRegistered or McpClientStatus.NeedsUpdate;

    private bool CanUnregister() => IsRegistered;
}
