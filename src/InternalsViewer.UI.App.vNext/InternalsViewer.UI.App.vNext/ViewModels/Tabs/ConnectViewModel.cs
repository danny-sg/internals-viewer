using System;
using InternalsViewer.UI.App.vNext.Services;
using System.Threading.Tasks;
using InternalsViewer.UI.App.vNext.Models.Connections;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class ConnectViewModel(MainViewModel parent) : TabViewModel(parent, TabType.Connect)
{
    public static async Task<ConnectViewModel> CreateAsync(MainViewModel parent)
    {
        var viewModel = new ConnectViewModel(parent);
        
        await viewModel.Initialize();
        viewModel.Name = "Connect";

        return viewModel;
    }

    private async Task Initialize()
    {
        var settingsService = GetSettingsService();

        ServerConnectionSettings = await settingsService.ReadSettingAsync<ServerConnectionSettings>("CurrentServerConnection");
    }

    public async Task SaveSettings(ServerConnectionSettings? connectionSettings)
    {
        var settingsService = GetSettingsService();

        await settingsService.SaveSettingAsync("CurrentServerConnection", connectionSettings);
    }

    private SettingsService GetSettingsService()
    {
        var settingsService = Parent.GetService<SettingsService>();

        return settingsService;
    }

    [ObservableProperty]
    private ServerConnectionSettings? serverConnectionSettings;
}
