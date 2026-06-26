using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Index;

public sealed class IndexTabViewModelFactory(ILogger<IndexTabViewModel> logger,
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
    private float _zoom = 1;

    [ObservableProperty]
    private PageAddress _rootPage;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isRecordsLoading;

    private const int RecordsSpinnerDelayMs = 100;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private int _objectId;

    [ObservableProperty]
    private int _indexId;

    [ObservableProperty]
    private string _indexName = string.Empty;

    [ObservableProperty]
    private string _objectIndexType = string.Empty;

    [ObservableProperty]
    private string _indexType = string.Empty;

    [ObservableProperty]
    private bool _isTooltipEnabled;

    [ObservableProperty]
    private Visibility _indexDetailVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _records = [];

    [ObservableProperty]
    private PageAddress? _selectedPageAddress;

    [ObservableProperty]
    private PageAddress? _selectedNextPage;

    [ObservableProperty]
    private PageAddress? _selectedPreviousPage;

    [ObservableProperty]
    private int? _selectedLevel;

    [ObservableProperty]
    private ObservableCollection<PageAddress> _highlightedPages = [];

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

                Logger.LogDebug("Getting nodes for index from root node: {RootPage}", RootPage);

                var result = await IndexService.GetNodes(Database, RootPage);

                Logger.LogDebug("{Count} node(s) found", result.Count);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogDebug("Updating UI");

                    Nodes = result;
                    IsInitialized = true;
                });
            });
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress pageAddress)
    {
        Logger.LogDebug("Loading Index Page: {PageAddress}", pageAddress);

        if (pageAddress == PageAddress.Empty)
        {
            Logger.LogDebug("(Page Empty)");

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

        SelectedPageAddress = pageAddress;

        IndexDetailVisibility = Visibility.Visible;

        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowRecordsSpinnerAfterDelay(spinnerDelay.Token);

        Internals.Engine.Pages.Page? page = null;

        List<IndexRecordModel> decodedRecords = [];

        // Worker thread
        await Task.Run(async () =>
            {
                Logger.LogDebug("Loading Page: {PageAddress}", pageAddress);

                page = await PageService.GetPage(Database, pageAddress);

                if (page is IndexPage indexPage)
                {
                    Logger.LogDebug("Decoding Index Page records");

                    decodedRecords = GetIndexRecordModels(RecordService.GetIndexRecords(indexPage,
                                                                                        isMarkEnabled: true));
                }
                else if (page is DataPage dataPage)
                {
                    Logger.LogDebug("Decoding Data Page records");

                    decodedRecords = GetDataRecordModels(RecordService.GetDataRecords(dataPage, isMarkEnabled: true));
                }
            });

        Logger.LogDebug("Decoded {Count} record(s)", decodedRecords.Count);

        await spinnerDelay.CancelAsync();

        Records = new ObservableCollection<IndexRecordModel>(decodedRecords);
        SelectedLevel = page?.PageHeader.Level;
        SelectedNextPage = page?.PageHeader.NextPage;
        SelectedPreviousPage = page?.PageHeader.PreviousPage;

        IsRecordsLoading = false;

        IndexDetailVisibility = Visibility.Visible;
    }

    private async Task ShowRecordsSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(RecordsSpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsRecordsLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // Load completed within the delay window
        }
    }

    partial void OnRootPageChanged(PageAddress value)
    {
        var allocationUnit = Database.AllocationUnits.Values.FirstOrDefault(a => a.RootPage == value);

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

    private static List<IndexRecordModel> GetIndexRecordModels(IEnumerable<IIndexRecord> source)
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

    private static List<IndexRecordModel> GetDataRecordModels(IEnumerable<IRecord> source)
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
