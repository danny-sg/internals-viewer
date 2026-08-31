using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.Controls.Columnstore.Structure;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed class ColumnstoreTabViewModelFactory(ILogger<ColumnstoreTabViewModel> logger,
                                                   ILoggerFactory loggerFactory,
                                                   ColumnstoreService columnstoreService,
                                                   IPageService pageService,
                                                   IIamChainService iamChainService,
                                                   IRecordService recordService)
{
    public ColumnstoreTabViewModel Create(DatabaseSource database, long allocationUnitId)
    {
        var allocationUnit = database.AllocationUnits.GetValueOrDefault(allocationUnitId)
                             ?? database.AllocationUnits
                                 .Values
                                 .FirstOrDefault(a => a.AllocationUnitId == allocationUnitId);

        if (allocationUnit is null)
        {
            throw new InvalidOperationException($"Allocation unit: {allocationUnitId} not found");
        }

        return new(logger,
                   loggerFactory,
                   columnstoreService,
                   pageService,
                   iamChainService,
                   recordService,
                   database,
                   allocationUnit);
    }
}

public sealed partial class ColumnstoreTabViewModel : TabViewModel
{
    private const int SpinnerDelayMs = 100;

    private const int DictionariesTabIndex = 1;

    [ObservableProperty]
    private ColumnStoreIndex? _index;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isStructureLoading;

    [ObservableProperty]
    private string _loadingText = "Loading Columnstore Index...";

    [ObservableProperty]
    private string _indexDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowGroupCountDescription))]
    private int _rowGroupCount;

    [ObservableProperty]
    private IReadOnlyList<RowGroupSummary> _rowGroups = [];

    [ObservableProperty]
    private IReadOnlyList<SegmentSummary> _segments = [];


    /// <remarks>
    /// A tab holding its own content is rebuilt every time it is selected, so the strip is separated from what it
    /// picks and the grids are shown and hidden instead.
    /// </remarks>
    [ObservableProperty]
    private int _selectedMetadataTabIndex;

    [ObservableProperty]
    private bool _isDictionariesTabLoaded;

    /// <summary>
    /// Whether the index is the table or an index over it, which decides whether it carries a row locator
    /// </summary>
    [ObservableProperty]
    private string _indexTypeDescription = string.Empty;

    /// <summary>
    /// Every dictionary the index holds, global ones once and local ones per row group
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<DictionarySummary> _dictionaries = [];

    [ObservableProperty]
    private IReadOnlyDictionary<long, SubLobType> _dictionaryCoding = new Dictionary<long, SubLobType>();

    /// <summary>
    /// Bumped as headers and coding arrive, which is what tells the drawing there is something new to paint
    /// </summary>
    /// <remarks>
    /// The drawing reads the summaries rather than binding to them, so a header landing changes what it would draw
    /// without anything asking it to draw again. One read at a time means the repaints are spaced by the reads.
    /// </remarks>
    [ObservableProperty]
    private int _drawingRevision;

    /// <summary>
    /// Whether every background read the drawing shows has arrived, the structure staying behind a spinner until it has
    /// </summary>
    [ObservableProperty]
    private bool _isDrawingReady;

    public ColumnstoreTabViewModel(ILogger<ColumnstoreTabViewModel> logger,
                                   ILoggerFactory loggerFactory,
                                   ColumnstoreService columnstoreService,
                                   IPageService pageService,
                                   IIamChainService iamChainService,
                                   IRecordService recordService,
                                   DatabaseSource database,
                                   AllocationUnit allocationUnit)
    {
        Logger = logger;
        LoggerFactory = loggerFactory;
        ColumnstoreService = columnstoreService;
        PageService = pageService;
        IamChainService = iamChainService;
        RecordService = recordService;
        Database = database;
        AllocationUnit = allocationUnit;

        Dock = BuildDock();
    }

    public DatabaseSource Database { get; }

    public AllocationUnit AllocationUnit { get; }

    public string RowGroupCountDescription => RowGroupCount == 1 ? "1 row group" : $"{RowGroupCount} row groups";

    public short DatabaseId => Database.DatabaseId;

    internal ColumnstoreService ColumnstoreService { get; }

    internal IPageService PageService { get; }

    internal IIamChainService IamChainService { get; }

    internal IRecordService RecordService { get; }

    private ILogger<ColumnstoreTabViewModel> Logger { get; }

    /// <summary>
    /// Hands each tab a logger of its own, which is what the timings are written through
    /// </summary>
    private ILoggerFactory LoggerFactory { get; }

    public async Task Load()
    {
        IsLoading = true;
        IsInitialized = false;
        IsDrawingReady = false;

        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        try
        {
            Name = string.IsNullOrEmpty(AllocationUnit.DisplayName)
                   ? AllocationUnit.TableName
                   : AllocationUnit.DisplayName;
                  
            var index = await Task.Run(() => ColumnstoreService.GetIndex(AllocationUnit, Database, CancellationToken),
                                       CancellationToken);

            var summaries = await Task.Run(() => RowGroupSummary.Build(index), CancellationToken);

            await spinnerDelay.CancelAsync();

            Index = index;

            RowGroups = summaries;

            Segments = [.. summaries.SelectMany(s => s.Segments)];

            IndexTypeDescription = index.IsClustered ? "Clustered" : "Non-Clustered";

            BuildDictionaries(index);

            IndexDescription = string.IsNullOrEmpty(index.IndexName)
                ? $"{index.SchemaName}.{index.TableName}"
                : $"{index.SchemaName}.{index.TableName}.{index.IndexName}";

            RowGroupCount = RowGroups.Count;

            IsInitialized = true;

            _ = LoadSegmentHeaders();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load columnstore index {AllocationUnitId}", AllocationUnit.AllocationUnitId);

            LoadingText = exception.Message;
        }
        finally
        {
            await spinnerDelay.CancelAsync();

            IsStructureLoading = false;

            IsLoading = false;
        }
    }

    partial void OnSelectedMetadataTabIndexChanged(int value)
    {
        if (value == DictionariesTabIndex)
        {
            IsDictionariesTabLoaded = true;
        }
    }

    /// <summary>
    /// Reads the prologue of every segment blob, which the metadata does not carry
    /// </summary>
    /// <remarks>
    /// The structure type and the RLE and bit pack counts only exist inside the blob, and a prologue costs a couple
    /// of page reads against the whole blob's many. It still runs after the index is on screen rather than holding
    /// it up, the drawing standing on its own without them.
    /// </remarks>
    private async Task LoadSegmentHeaders()
    {
        await LoadDictionaryCoding();

        var segments = Segments.Where(s => s.HasDataPointer).ToList();

        foreach (var segment in segments)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var header = await Task.Run(
                    () => ColumnstoreService.GetSegmentHeader(Database, segment.Segment, CancellationToken),
                    CancellationToken);

                segment.Header = header;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Logger.LogDebug(exception,
                                "Could not read the segment header for row group {RowGroup} column {Column}",
                                segment.RowGroupId,
                                segment.ColumnId);
            }
        }

        DrawingRevision++;

        IsDrawingReady = true;

        await LoadSegmentRuns();
    }

    private async Task LoadSegmentRuns()
    {
        foreach (var segment in Segments.Where(s => s.HasDataPointer && s.Runs.Count == 0).ToList())
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var blob = await Task.Run(() => ColumnstoreService.GetSegmentBlob(Database,
                                                                                  segment.Segment,
                                                                                  CancellationToken,
                                                                                  depth: SegmentLoadDepth.Runs),
                                          CancellationToken);

                segment.Runs = blob.RleEntries;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Logger.LogDebug(exception,
                                "Could not read the segment runs for row group {RowGroup} column {Column}",
                                segment.RowGroupId,
                                segment.ColumnId);
            }
        }

        DrawingRevision++;
    }

    /// <summary>
    /// How each dictionary's pages are coded, which the drawing shows as a badge once it arrives
    /// </summary>
    private async Task LoadDictionaryCoding()
    {
        var dictionaries = Index?.Columns
                                .Select(c => c.GlobalDictionary)
                                .Concat(Segments.Select(s => s.LocalDictionary))
                                .Where(d => d is not null)
                                .GroupBy(d => (d!.ColumnId, d.DictionaryId))
                                .Select(g => g.First()!)
                                .ToList() ?? [];

        var coding = new Dictionary<long, SubLobType>();

        var summariesByKey = new Dictionary<(int ColumnId, int DictionaryId), DictionarySummary>();

        foreach (var summary in Dictionaries)
        {
            summariesByKey.TryAdd((summary.ColumnId, summary.DictionaryId), summary);
        }

        foreach (var dictionary in dictionaries)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var pageCoding = await Task.Run(
                    () => ColumnstoreService.GetDictionaryCoding(Database, dictionary, CancellationToken),
                    CancellationToken);

                if (pageCoding.Coding is { } value)
                {
                    coding[ColumnstoreStructureRenderer.CodingKey(dictionary.ColumnId, dictionary.DictionaryId)] = value;
                }

                if (summariesByKey.TryGetValue((dictionary.ColumnId, dictionary.DictionaryId), out var summary))
                {
                    summary.PageCount = pageCoding.PageCount;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Logger.LogDebug(exception,
                                "Could not read the coding for column {Column} dictionary {Dictionary}",
                                dictionary.ColumnId,
                                dictionary.DictionaryId);
            }
        }

        DictionaryCoding = coding;

        DrawingRevision++;
    }

    private void BuildDictionaries(ColumnStoreIndex index)
    {
        var dictionaries = new List<DictionarySummary>();

        foreach (var column in index.Columns)
        {
            if (column.GlobalDictionary is { } global)
            {
                dictionaries.Add(new DictionarySummary { Dictionary = global, ColumnName = column.Name });
            }
        }

        foreach (var segment in index.RowGroups.SelectMany(r => r.Segments))
        {
            if (segment.LocalDictionary is { } local)
            {
                dictionaries.Add(new DictionarySummary
                {
                    Dictionary = local,
                    ColumnName = segment.Column?.Name ?? $"Column {local.ColumnId}"
                });
            }
        }

        Dictionaries = dictionaries;
    }

    /// <summary>
    /// Holds the spinner back so a load that returns straight away does not flash one up
    /// </summary>
    private async Task ShowSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsStructureLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // The load finished inside the delay, so no spinner is wanted
        }
    }

    [RelayCommand]
    private async Task Refresh() => await Load();
}
