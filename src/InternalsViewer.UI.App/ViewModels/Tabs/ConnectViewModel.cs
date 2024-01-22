using System;
using InternalsViewer.UI.App.Services;
using System.Threading.Tasks;
using InternalsViewer.UI.App.Models.Connections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class ConnectViewModel(IServiceProvider serviceProvider) : TabViewModel(serviceProvider, TabType.Connect)
{
    public static async Task<ConnectViewModel> CreateAsync(IServiceProvider serviceProvider)
    {
        var viewModel = new ConnectViewModel(serviceProvider);
        
        await viewModel.InitializeAsync();
        viewModel.Name = "Connect";

        return viewModel;
    }

    public async Task InitializeAsync()
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
        var settingsService = GetService<SettingsService>();

        return settingsService;
    }

    [ObservableProperty]
    private ServerConnectionSettings? serverConnectionSettings;
}
