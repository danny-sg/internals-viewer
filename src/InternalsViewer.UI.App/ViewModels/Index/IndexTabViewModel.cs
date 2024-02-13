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
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.Internals.Engine.Records.Data;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Index;

public class IndexTabViewModelFactory(ILogger<IndexTabViewModel> logger,
                                      IndexService indexService,
                                      IPageService pageService,
                                      IRecordService recordService)
{
    private IndexService IndexService { get; } = indexService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public IndexTabViewModel Create(DatabaseSource database)
        => new(logger, IndexService, RecordService, PageService, database);
}

public partial class IndexTabViewModel(ILogger<IndexTabViewModel> logger,
                                       IndexService indexService,
                                       IRecordService recordService,
                                       IPageService pageService,
                                       DatabaseSource database) : TabViewModel
{
    private ILogger<IndexTabViewModel> Logger { get; } = logger;

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

    [RelayCommand]
    public async Task Refresh()
    {
        Logger.LogDebug("Refreshing Index Tab");

        // Worker thread
        await Task.Run(async () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    IsInitialized = false;
                });

                Logger.LogDebug($"Getting nodes for index from root node: {RootPage}");

                var result = await IndexService.GetNodes(Database, RootPage);

                Logger.LogDebug($"{result.Count} node(s) found");

                DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogDebug($"Updating UI");

                    Nodes = result;
                    IsInitialized = true;
                });
            });
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress pageAddress)
    {
        Logger.LogDebug($"Loading Index Page: {pageAddress}");

        if (pageAddress == PageAddress.Empty)
        {
            Logger.LogDebug($"(Page Empty)");

            // Update via UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                IndexDetailVisibility = Visibility.Collapsed;

                SelectedLevel = null;
                SelectedNextPage = null;
                SelectedPreviousPage = null;
                SelectedPageAddress = pageAddress;

                HighlightedPages.Clear();
                Records.Clear();
            });

            return;
        }

        // Update via UI thread
        DispatcherQueue.TryEnqueue(() =>
            {
                SelectedPageAddress = pageAddress;
                IsInitialized = false;
            });

        Internals.Engine.Pages.Page? page = null;

        List<IndexRecordModel> decodedRecords = new();

        // Worker thread
        await Task.Run(async () =>
            {
                Logger.LogDebug($"Loading Page: {pageAddress}");

                page = await PageService.GetPage(Database, pageAddress);

                if (page is IndexPage indexPage)
                {
                    Logger.LogDebug($"Decoding Index Page records");

                    decodedRecords = GetIndexRecordModels(RecordService.GetIndexRecords(indexPage));
                }
                else if (page is DataPage dataPage)
                {
                    Logger.LogDebug($"Decoding Data Page records");

                    decodedRecords = GetDataRecordModels(RecordService.GetDataRecords(dataPage));
                }
            });

        Logger.LogDebug($"Decoded {records.Count} record(s)");

        // Update via UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            Records = new ObservableCollection<IndexRecordModel>(decodedRecords);
            SelectedLevel = page?.PageHeader.Level;
            SelectedNextPage = page?.PageHeader.NextPage;
            SelectedPreviousPage = page?.PageHeader.PreviousPage;

            IsInitialized = true;

            IndexDetailVisibility = Visibility.Visible;
        });
    }

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

    private static List<IndexRecordModel> GetIndexRecordModels(IEnumerable<IndexRecord> source)
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
        }).ToList();

        return models;
    }

    private static List<IndexRecordModel> GetDataRecordModels(IEnumerable<DataRecord> source)
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
        }).ToList();

        return models;
    }

    public void SetHighlightedPage(PageAddress pageAddress)
    {
        if (pageAddress != PageAddress.Empty)
        {
            HighlightedPages = [pageAddress];
        }
        else
        {
            HighlightedPages = [];
        }
    }
}
