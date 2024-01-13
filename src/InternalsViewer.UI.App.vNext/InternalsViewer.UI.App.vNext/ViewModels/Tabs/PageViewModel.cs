using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class PageViewModel(MainViewModel parent, 
                                   DatabaseSource database) : TabViewModel(parent, TabType.Page)
{
    public override TabType TabType => TabType.Page;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    private Internals.Engine.Pages.Page? page;

    [ObservableProperty]
    private DatabaseSource database = database;

    public async Task LoadPage(PageAddress address)
    {
        IsLoading = true;

        var pageService = Parent.ServiceProvider.GetService<IPageService>();

        if(pageService == null)
        {
            throw new InvalidOperationException("Page Service not registered");
        }

        Name = $"Page {address}";
        PageAddress = address;

        Page = await pageService.GetPage(Database, address);

        IsLoading = false;
    }
}
