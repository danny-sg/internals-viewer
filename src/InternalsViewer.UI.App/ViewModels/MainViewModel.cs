using System;
using InternalsViewer.UI.App.Services;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models.Connections;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.ViewModels;

public partial class MainViewModel(IServiceProvider serviceProvider, SettingsService settingsService) : ObservableObject
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    private SettingsService SettingsService { get; } = settingsService;

    public T GetService<T>()
    {
        var service = ServiceProvider.GetService<T>();

        if (service is null)
        {
            throw new InvalidOperationException($"Service {typeof(T).Name} not found");
        }

        return service;
    }

    public async Task InitializeAsync()
    {
        var recent = await settingsService.ReadSettingAsync<RecentConnection[]>("RecentConnections");

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

        await settingsService.SaveSettingAsync("RecentConnections", RecentConnections.ToArray());
    }

    [RelayCommand]
    private async Task RemoveRecentConnection(string id)
    {
        var existing = RecentConnections.Where(c => c.Id != id).ToList();

        RecentConnections = new ObservableCollection<RecentConnection>(existing);

        await settingsService.SaveSettingAsync("RecentConnections", RecentConnections.ToArray());
    }

    [ObservableProperty]
    private ObservableCollection<RecentConnection> recentConnections = new();
}
