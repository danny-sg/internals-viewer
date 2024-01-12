using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class PageViewModel(MainViewModel parent, DatabaseSource database) : TabViewModel(parent, TabType.Page)
{
    public override TabType TabType => TabType.Page;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    private DatabaseSource database = database;

    public async Task LoadPage(PageAddress address)
    {
       Name = $"Page {address}";
        PageAddress = address;
    }
}
