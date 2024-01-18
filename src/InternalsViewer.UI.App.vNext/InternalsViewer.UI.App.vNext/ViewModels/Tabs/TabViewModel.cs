using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject
{
    public IServiceProvider ServiceProvider { get; }
    public virtual TabType TabType { get; }

    [ObservableProperty]
    private string tabId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isLoading = true;

    public virtual ImageSource ImageSource => new BitmapImage(new Uri("ms-appx:///Assets/TabIcons/Database16.png"));

    public TabViewModel(IServiceProvider serviceProvider, TabType tabType)
    {
        ServiceProvider = serviceProvider;
        TabType = tabType;
        TabId = Guid.NewGuid().ToString();
    }

    public T GetService<T>()
    {
        var service = ServiceProvider.GetService<T>();

        if (service is null)
        {
            throw new InvalidOperationException($"Service {typeof(T).Name} not found");
        }

        return service;
    }
}