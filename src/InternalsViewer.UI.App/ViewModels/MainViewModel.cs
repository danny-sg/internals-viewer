using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Tabs;

namespace InternalsViewer.UI.App.ViewModels;

public partial class MainViewModel(SettingsService settingsService)
    : TabViewModel
{
    private SettingsService SettingsService { get; } = settingsService;

    public async Task InitializeAsync()
    {
        var recent = await SettingsService.ReadSettingAsync<RecentConnection[]>("RecentConnections");

        if (recent != null)
        {
            RecentConnections = new ObservableCollection<RecentConnection>(recent);
        }


        var bookmarks = await SettingsService.ReadSettingAsync<PageBookmark[]>("PageBookmarks");

        if (bookmarks != null)
        {
            PageBookmarks = new ObservableCollection<PageBookmark>(bookmarks);
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
    private async Task ClearRecentConnections()
    {
        RecentConnections = new ObservableCollection<RecentConnection>([]);

        await SettingsService.SaveSettingAsync("RecentConnections", Array.Empty<RecentConnection>());
    }

    [RelayCommand]
    private async Task RemoveRecentConnection(string id)
    {
        var existing = RecentConnections.Where(c => c.Id != id).ToList();

        RecentConnections = new ObservableCollection<RecentConnection>(existing);

        await SettingsService.SaveSettingAsync("RecentConnections", RecentConnections.ToArray());
    }

    [RelayCommand]
    private async Task ConnectRecent(RecentConnection recent)
    {
        switch (recent.ConnectionType)
        {
            case "Server":
                var serverMessage = new ConnectServerMessage(recent.Value, recent);

                serverMessage.IsPasswordRequired = recent.IsPasswordRequired;

                await WeakReferenceMessenger.Default.Send(serverMessage);

                break;

            case "File":
                var fileMessage = new ConnectFileMessage(recent.Value, recent);

                await WeakReferenceMessenger.Default.Send(fileMessage);

                break;

            case "Backup":
                var backupMessage = new ConnectFileMessage(recent.Value, recent);

                await WeakReferenceMessenger.Default.Send(backupMessage);

                break;
        }
    }

    [RelayCommand]
    private async Task AddBookmark(PageBookmark bookmark)
    {
        if (PageBookmarks.All(b => b != bookmark))
        {
            PageBookmarks.Add(bookmark);

            await SettingsService.SaveSettingAsync("PageBookmarks", PageBookmarks.ToArray());
        }
    }

    [RelayCommand]
    private async Task RemoveBookmark(PageBookmark bookmark)
    {
        PageBookmarks.Remove(bookmark);

        await SettingsService.SaveSettingAsync("PageBookmarks", PageBookmarks.ToArray());
    }

    [ObservableProperty]
    private ObservableCollection<RecentConnection> _recentConnections = [];

    [ObservableProperty]
    private ObservableCollection<PageBookmark> _pageBookmarks = [];
}