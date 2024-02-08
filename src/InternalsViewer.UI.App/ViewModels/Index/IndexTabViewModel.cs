using System.Threading.Tasks;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.Internals.Services.Indexes;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Address;
using System.Collections.Generic;
using System.Linq;

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
    private PageAddress rootPage;

    [ObservableProperty]
    private List<IndexNode> nodes = new();

    [ObservableProperty]
    private bool isInitialized;

    [ObservableProperty]
    private string objectName = string.Empty;

    [ObservableProperty]
    private int objectId;

    [ObservableProperty]
    private int indexId;

    [ObservableProperty]
    private string indexName = string.Empty;

    [ObservableProperty]
    private string objectIndexType = string.Empty;

    [ObservableProperty]
    private string indexType = string.Empty;

    partial void OnRootPageChanged(PageAddress value)
    {
        var allocationUnit = Database.AllocationUnits.FirstOrDefault(a => a.RootPage == value);
        
        if(allocationUnit != null)
        {
            SetAllocationUnitDescription(allocationUnit);
        }
    }

    private void SetAllocationUnitDescription(AllocationUnit allocationUnit)
    {
        ObjectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";
        ObjectId = allocationUnit.ObjectId;

        IndexName = allocationUnit.IndexName;
        IndexId = allocationUnit.IndexId;

        IndexType = allocationUnit.IndexType == Internals.Engine.Database.Enums.IndexType.NonClustered
            ? "Non-Clustered"
            : string.Empty;
        ObjectIndexType = allocationUnit.ParentIndexType == Internals.Engine.Database.Enums.IndexType.Clustered
            ? "Clustered"
            : "Heap";

        Name = "Index: " + IndexName;
    }

    public async Task Initialize()
    {
        Nodes = await IndexService.GetNodes(Database, RootPage);

        IsInitialized = true;
    }   
}
