using System.Threading.Tasks;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.Internals.Services.Indexes;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Address;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.ViewModels.Index;

public class IndexTabViewModelFactory(IndexService indexService)
{
    public IndexService IndexService { get; } = indexService;
    
    public IndexTabViewModel Create(DatabaseSource database)
        => new(indexService, database);
}

public partial class IndexTabViewModel(IndexService indexService, DatabaseSource database) : TabViewModel
{
    private IndexService IndexService { get; } = indexService;

    private DatabaseSource Database { get; } = database;

    [ObservableProperty]
    private float zoom = 1;

    [ObservableProperty]
    private List<IndexNode> nodes = new();

    public async Task LoadIndex(PageAddress rootPage)
    {
        Nodes = await IndexService.GetNodes(Database, rootPage);

        Name = "Index: " + rootPage; 
    }   
}
