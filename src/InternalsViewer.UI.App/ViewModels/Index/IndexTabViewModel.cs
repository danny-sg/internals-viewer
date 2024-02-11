using System.Threading.Tasks;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.Internals.Services.Indexes;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Address;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Interfaces.Services.Records;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.Internals.Engine.Records.Data;

namespace InternalsViewer.UI.App.ViewModels.Index;

public class IndexTabViewModelFactory(IndexService indexService,
                                      IPageService pageService,
                                      IRecordService recordService)
{
    private IndexService IndexService { get; } = indexService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public IndexTabViewModel Create(DatabaseSource database)
        => new(IndexService, RecordService, PageService, database);
}

public partial class IndexTabViewModel(IndexService indexService,
                                       IRecordService recordService,
                                       IPageService pageService,
                                       DatabaseSource database) : TabViewModel
{
    private IndexService IndexService { get; } = indexService;

    private IRecordService RecordService { get; } = recordService;

    private IPageService PageService { get; } = pageService;

    public DatabaseSource Database { get; } = database;

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

    [ObservableProperty]
    private bool isTooltipEnabled;

    [ObservableProperty]
    private Visibility indexDetailVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> records = new();

    [ObservableProperty]
    private PageAddress? selectedPageAddress;

    [ObservableProperty]
    private PageAddress? selectedNextPage;

    [ObservableProperty]
    private PageAddress? selectedPreviousPage;

    [ObservableProperty]
    private int? selectedLevel;

    [ObservableProperty]
    private ObservableCollection<PageAddress> highlightedPages = new();

    partial void OnRootPageChanged(PageAddress value)
    {
        var allocationUnit = Database.AllocationUnits.FirstOrDefault(a => a.RootPage == value);

        if (allocationUnit != null)
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

    [RelayCommand]
    private async Task LoadPage(PageAddress pageAddress)
    {
        SelectedPageAddress = pageAddress;

        if (pageAddress == PageAddress.Empty)
        {
            IndexDetailVisibility = Visibility.Collapsed;
            
            SelectedLevel = null;
            SelectedNextPage = null;
            SelectedPreviousPage = null;

            HighlightedPages.Clear();
            Records.Clear();

            return;
        }

        IsInitialized = false;

        var page = await PageService.GetPage(Database, pageAddress);

        SelectedLevel = page.PageHeader.Level;
        SelectedNextPage = page.PageHeader.NextPage;
        SelectedPreviousPage = page.PageHeader.PreviousPage;

        if (page is IndexPage indexPage)
        {
            Records = GetIndexRecordModels(RecordService.GetIndexRecords(indexPage));
        }
        else if (page is DataPage dataPage)
        {
            Records = GetDataRecordModels(RecordService.GetDataRecords(dataPage));
        }

        IsInitialized = true;

        IndexDetailVisibility = Visibility.Visible;
    }

    private ObservableCollection<IndexRecordModel> GetIndexRecordModels(IEnumerable<IndexRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            DownPagePointer = r.DownPagePointer,
            RowIdentifier = r.Rid,
            Fields = r.Fields.Select(f => new IndexRecordFieldModel
            {
                Name = f.Name,
                Value = f.Value,
                DataType = f.ColumnStructure.DataType
            }).ToList()
        });

        return new ObservableCollection<IndexRecordModel>(models);
    }

    private ObservableCollection<IndexRecordModel> GetDataRecordModels(IEnumerable<DataRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            Fields = r.Fields.Select(f => new IndexRecordFieldModel
            {
                Name = f.Name,
                Value = f.Value,
                DataType = f.ColumnStructure.DataType
            }).ToList()
        });

        return new ObservableCollection<IndexRecordModel>(models);
    }

    public void SetHighlightedPage(PageAddress pageAddress)
    {
        if (pageAddress != PageAddress.Empty)
        {
            HighlightedPages = new ObservableCollection<PageAddress> { pageAddress };
        }
        else
        {
            HighlightedPages = new ObservableCollection<PageAddress>();
        }
    }
}
