using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject
{
    private IServiceProvider ServiceProvider { get; }

    [ObservableProperty]
    private string tabId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isLoading = true;

    protected TabViewModel(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        TabId = Guid.NewGuid().ToString();
    }

    protected T GetService<T>()
    {
        var service = ServiceProvider.GetService<T>();

        if (service is null)
        {
            throw new InvalidOperationException($"Service {typeof(T).Name} not found");
        }

        return service;
    }
}