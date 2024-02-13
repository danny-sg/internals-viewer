using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject
{
    [ObservableProperty]
    private string tabId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isLoading = true;

    protected DispatcherQueue DispatcherQueue { get; }

    protected TabViewModel()
    {
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        TabId = Guid.NewGuid().ToString();
    }
}