using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.ViewModels.Index;

public sealed class IndexTabViewModelFactory(ILogger<IndexTabViewModel> logger,
                                             IndexService indexService,
                                             IPageService pageService,
                                             IRecordService recordService,
                                             IndexStepService indexStepService)
{
    private IndexService IndexService { get; } = indexService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    private IndexStepService IndexStepService { get; } = indexStepService;

    public IndexTabViewModel Create(DatabaseSource database)
        => new(logger, IndexService, RecordService, PageService, IndexStepService, database);
}

public partial class IndexTabViewModel(ILogger<IndexTabViewModel> logger,
                                       IndexService indexService,
                                       IRecordService recordService,
                                       IPageService pageService,
                                       IndexStepService indexStepService,
                                       DatabaseSource database) : TabViewModel
{
    private ILogger<IndexTabViewModel> Logger { get; } = logger;

    private IndexService IndexService { get; } = indexService;

    private IRecordService RecordService { get; } = recordService;

    private IPageService PageService { get; } = pageService;

    private IndexStepService IndexStepService { get; } = indexStepService;

    public DatabaseSource Database { get; } = database;

    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _isZoomToFit = true;

    [ObservableProperty]
    private PageAddress _rootPage;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private int _loadedPageCount;

    [ObservableProperty]
    private long _totalPageCount;

    [ObservableProperty]
    private AllocationUnit? _allocationUnit;

    public string ProgressText => TotalPageCount > 0
        ? $"{LoadedPageCount:N0} / {TotalPageCount:N0} pages"
        : $"{LoadedPageCount:N0} pages";

    public bool IsProgressVisible => TotalPageCount >= IndexService.ProgressReportInterval;

    public double ProgressMaximum => TotalPageCount;

    public string LoadingText => string.IsNullOrEmpty(AllocationUnit?.IndexName)
        ? "Loading Index..."
        : $"Loading {AllocationUnit.IndexName}...";

    partial void OnAllocationUnitChanged(AllocationUnit? value) => OnPropertyChanged(nameof(LoadingText));

    partial void OnLoadedPageCountChanged(int value) => OnPropertyChanged(nameof(ProgressText));

    partial void OnTotalPageCountChanged(long value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(ProgressMaximum));
    }

    [ObservableProperty]
    private bool _isRecordsLoading;

    private const int RecordsSpinnerDelayMs = 100;

    [ObservableProperty]
    private string _objectIndexType = string.Empty;
    
    [ObservableProperty]
    private bool _isTooltipEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailSplitterVisibility))]
    private bool _isDetailPaneVisible;

    public GridLength BodyColumnWidth
        => IsDetailPaneVisible ? new GridLength(6, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsDetailPaneVisible ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailSplitterVisibility
        => IsDetailPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _records = [];

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _resultRecords = [];

    [ObservableProperty]
    private PageAddress? _selectedPageAddress;

    [ObservableProperty]
    private IReadOnlyList<PageSpan> _pageSpans = [];

    [ObservableProperty]
    private long _playheadTimeUs;

    [ObservableProperty]
    private PageAddress? _selectedNextPage;

    [ObservableProperty]
    private PageAddress? _selectedPreviousPage;

    [ObservableProperty]
    private int? _selectedLevel;

    [ObservableProperty]
    private int? _selectedSlot;

    [ObservableProperty]
    private ObservableCollection<PageAddress> _highlightedPages = [];

    [ObservableProperty]
    private PlanNode? _planNode;

    public event EventHandler<PageAddress>? PageNavigated;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private SeekStrategy? _strategy;

    public SeekPhase? CurrentPhase => CurrentStep?.SeekPhase;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(CurrentCounters));
    }

    partial void OnIsSteppingChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    partial void OnIsStepCompleteChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    private const int RunStepDelayMs = 150;

    partial void OnPlanNodeChanged(PlanNode? value)
    {
        ClearStepState();
        
        OnPropertyChanged(nameof(PredicateText));
        OnPropertyChanged(nameof(IconSource));
        OnPropertyChanged(nameof(HasPredicate));
    }

    public PredicateText? PredicateText => PlanNode?.GetText();

    public bool HasPredicate => (PredicateText?.Tokens.Length ?? 0) > 0;

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    [RelayCommand]
    public async Task Refresh()
    {
        Logger.LogDebug("Refreshing Index Tab");

        LoadedPageCount = 0;

        var progress = new Progress<int>(count => LoadedPageCount = count);

        // Worker thread
        await Task.Run(async () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    IsInitialized = false;
                });

                IndexService.ProgressReportInterval = TotalPageCount > 100_000 ? 4096 : 1;

                Logger.LogDebug("Getting nodes for index from root node: {RootPage}", RootPage);

                var result = await IndexService.GetNodes(Database, RootPage, CancellationToken, progress);

                Logger.LogDebug("{Count} node(s) found", result.Count);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogDebug("Updating UI");

                    Nodes = result;
                    IsInitialized = true;
                });
            }, CancellationToken);
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
                IsDetailPaneVisible = false;

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

        IsDetailPaneVisible = true;

        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowRecordsSpinnerAfterDelay(spinnerDelay.Token);

        Internals.Engine.Pages.Page? page = null;

        List<IndexRecordModel> decodedRecords = [];

        // Worker thread
        await Task.Run(async () =>
            {
                Logger.LogDebug("Loading Page: {PageAddress}", pageAddress);

                page = await PageService.GetPage(Database, pageAddress, CancellationToken);

                if (page is IndexPage indexPage)
                {
                    Logger.LogDebug("Decoding Index Page records");

                    decodedRecords = GetIndexRecordModels(RecordService.GetIndexRecords(indexPage));
                }
                else if (page is DataPage dataPage)
                {
                    Logger.LogDebug("Decoding Data Page records");

                    decodedRecords = GetDataRecordModels(RecordService.GetDataRecords(dataPage));
                }
            }, CancellationToken);

        Logger.LogDebug("Decoded {Count} record(s)", decodedRecords.Count);

        await spinnerDelay.CancelAsync();

        Records = new ObservableCollection<IndexRecordModel>(decodedRecords);
        SelectedLevel = page?.PageHeader.Level;
        SelectedNextPage = page?.PageHeader.NextPage;
        SelectedPreviousPage = page?.PageHeader.PreviousPage;

        IsRecordsLoading = false;

        IsDetailPaneVisible = true;
    }

    [RelayCommand]
    public void ToggleStep()
    {
        IsDetailPaneVisible = !IsDetailPaneVisible;
    }

    [RelayCommand]
    public async Task StartStep()
    {
        if (PlanNode?.PredicateInfo is not { } predicateInfo || AllocationUnit is null)
        {
            Logger.LogDebug("No predicate information available to step through");

            return;
        }

        ClearStepState();

        var bounds = predicateInfo.HasSeekBounds ? predicateInfo.SeekBounds[0] : SeekBounds.All;

        await Task.Run(() => IndexStepService.StartAsync(Database,
                                                         AllocationUnit.AllocationUnitId,
                                                         RootPage,
                                                         bounds,
                                                         predicateInfo.Residual,
                                                         ScanDirection.Forward,
                                                         CancellationToken),
                       CancellationToken);

        Strategy = IndexStepService.Strategy;

        IsStepping = true;

        await StepNext();
    }

    private int _runDelayMs;

    private const int HistoryLimit = 1000;

    private bool _isBatching;

    private readonly List<AccessStep> _batchedSteps = [];

    private readonly List<IndexRecordModel> _batchedResults = [];

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task Run()
    {
        if (IsRunning)
        {
            IsRunning = false;

            return;
        }

        ClearStepState();

        _runDelayMs = RunStepDelayMs;

        await RunLoop();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task RunToEnd()
    {
        if (IsRunning)
        {
            _runDelayMs = 0;

            return;
        }

        if (!IsStepping || IsStepComplete)
        {
            ClearStepState();
        }

        _runDelayMs = 0;

        await RunLoop();
    }

    private async Task RunLoop()
    {
        IsRunning = true;

        try
        {
            while (IsRunning && !IsStepComplete)
            {
                _isBatching = _runDelayMs == 0;

                await StepNext();

                if (!IsStepping || IsStepComplete)
                {
                    break;
                }

                if (_runDelayMs > 0)
                {
                    await Task.Delay(_runDelayMs, CancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            FlushBatchedSteps();

            IsRunning = false;
        }
    }

    private void FlushBatchedSteps()
    {
        if (!_isBatching)
        {
            return;
        }

        _isBatching = false;

        if (_batchedSteps.Count > 0)
        {
            var recent = _batchedSteps.Skip(Math.Max(0, _batchedSteps.Count - HistoryLimit))
                                      .Reverse();

            StepHistory = new ObservableCollection<AccessStep>(recent.Concat(StepHistory).Take(HistoryLimit));

            CurrentStep = _batchedSteps[^1];

            SelectedSlot = GetStepSlot(CurrentStep);
        }

        if (_batchedResults.Count > 0)
        {
            ResultRecords = new ObservableCollection<IndexRecordModel>(ResultRecords.Concat(_batchedResults));
        }

        _batchedSteps.Clear();
        _batchedResults.Clear();

        if (SelectedPageAddress is { } pageAddress)
        {
            PageNavigated?.Invoke(this, pageAddress);
        }
    }

    private static int? GetStepSlot(AccessStep step)
    {
        return step switch
        {
            AccessStep.Probe probe => probe.Middle,
            AccessStep.ProbeResult probeResult => probeResult.Slot,
            AccessStep.Row row => row.Slot,
            AccessStep.RangeEnd rangeEnd => rangeEnd.Slot,
            _ => null
        };
    }

    [RelayCommand]
    public void ResetStep()
    {
        IsRunning = false;

        ClearStepState();
    }

    private void ClearStepState()
    {
        _isBatching = false;
        _batchedSteps.Clear();
        _batchedResults.Clear();

        StepHistory = [];
        CurrentStep = null;
        SelectedSlot = null;
        ResultRecords = [];
        Strategy = null;
        IsStepComplete = false;
        IsStepping = false;
    }

    [RelayCommand]
    public async Task StepNext()
    {
        while (true)
        {
            if (!IsStepping)
            {
                await StartStep();

                return;
            }
            if (IsStepComplete)
            {
                return;
            }

            var step = await Task.Run(() => IndexStepService.StepNextAsync(CancellationToken), CancellationToken);

            if (step is null)
            {
                IsStepComplete = true;

                return;
            }

            if (_isBatching)
            {
                _batchedSteps.Add(step);
            }
            else
            {
                StepHistory.Insert(0, step);

                if (StepHistory.Count > HistoryLimit)
                {
                    StepHistory.RemoveAt(StepHistory.Count - 1);
                }

                CurrentStep = step;

                SelectedSlot = GetStepSlot(step);
            }

            if (step is AccessStep.Row { Outcome: RowOutcome.Match } match)
            {
                var record = Records.FirstOrDefault(r => r.Slot == match.Slot);

                if (record is not null)
                {
                    if (_isBatching)
                    {
                        _batchedResults.Add(record);
                    }
                    else
                    {
                        ResultRecords.Add(record);
                    }
                }
            }

            if (step is AccessStep.Stopped)
            {
                IsStepComplete = true;
            }

            if (step is AccessStep.Descend)
            {
                continue;
            }

            var pageAddress = IndexStepService.CurrentPageAddress;

            if (pageAddress is not null && pageAddress != SelectedPageAddress)
            {
                SelectedPageAddress = pageAddress;

                await LoadPage(pageAddress.Value);

                if (!_isBatching)
                {
                    PageNavigated?.Invoke(this, pageAddress.Value);
                }
            }

            break;
        }
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
        
        AllocationUnit = allocationUnit;

        Name = "Index: " + AllocationUnit?.IndexName;
    }

    private static List<IndexRecordModel> GetIndexRecordModels(IEnumerable<IIndexRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            DownPagePointer = r.DownPagePointer,
            RowIdentifier = r.Rid,
            Fields =
            [
                .. r.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
        }).ToList();

        return models;
    }

    private static List<IndexRecordModel> GetDataRecordModels(IEnumerable<IRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            Fields =
            [
                .. r.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
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
