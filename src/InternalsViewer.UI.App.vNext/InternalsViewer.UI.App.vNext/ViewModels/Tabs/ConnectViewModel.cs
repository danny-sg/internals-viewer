using InternalsViewer.UI.App.vNext.Services;
using System.Threading.Tasks;
using InternalsViewer.UI.App.vNext.Models.Connections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class ConnectViewModel(MainViewModel parent, SettingsService settingsService) : TabViewModel(parent, TabType.Connect)
{
    protected SettingsService SettingsService { get; } = settingsService;

    public static async Task<ConnectViewModel> CreateAsync(MainViewModel parent, SettingsService settingsService)
    {
        var viewModel = new ConnectViewModel(parent, settingsService);
        
        await viewModel.Initialize();
        viewModel.Name = "Connect";

        return viewModel;
    }

    private async Task Initialize()
    {
        ServerConnectionSettings = await SettingsService.ReadSettingAsync<ServerConnectionSettings>("CurrentServerConnection");
    }

    public async Task SaveSettings(ServerConnectionSettings? connectionSettings)
    {
        await SettingsService.SaveSettingAsync("CurrentServerConnection", connectionSettings);
    }

    [ObservableProperty]
    private ServerConnectionSettings? serverConnectionSettings;
}
