using InternalsViewer.UI.App.Services;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models.Connections;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace InternalsViewer.UI.App.ViewModels;

public partial class MainViewModel(SettingsService settingsService) 
    : ObservableObject
{
    private SettingsService SettingsService { get; } = settingsService;

    public async Task InitializeAsync()
    {
        var recent = await SettingsService.ReadSettingAsync<RecentConnection[]>("RecentConnections");

        if (recent != null)
        {
            RecentConnections = new ObservableCollection<RecentConnection>(recent);
        }
    }

    [RelayCommand]
    private async Task AddRecentConnection(RecentConnection newConnection)
    {
        var existing = RecentConnections.Where(c => !(c.Name == newConnection.Name
                                                      && c.ConnectionType == newConnection.ConnectionType))
                                        .ToList();

        existing.Insert(0, newConnection);

        RecentConnections = new ObservableCollection<RecentConnection>(existing);

        await SettingsService.SaveSettingAsync("RecentConnections", RecentConnections.ToArray());
    }

    [RelayCommand]
    private async Task RemoveRecentConnection(string id)
    {
        var existing = RecentConnections.Where(c => c.Id != id).ToList();

        RecentConnections = new ObservableCollection<RecentConnection>(existing);

        await SettingsService.SaveSettingAsync("RecentConnections", RecentConnections.ToArray());
    }

    [ObservableProperty]
    private ObservableCollection<RecentConnection> recentConnections = new();
}
