using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed class ColumnstoreTabViewModelFactory(ILogger<ColumnstoreTabViewModel> logger,
                                                   ColumnstoreService columnstoreService,
                                                   IPageService pageService,
                                                   IIamChainService iamChainService,
                                                   IRecordService recordService)
{
    public ColumnstoreTabViewModel Create(DatabaseSource database, long allocationUnitId)
        => new(logger, columnstoreService, pageService, iamChainService, recordService, database, allocationUnitId);
}

public sealed partial class ColumnstoreTabViewModel : TabViewModel
{
    public ColumnstoreTabViewModel(ILogger<ColumnstoreTabViewModel> logger,
                                   ColumnstoreService columnstoreService,
                                   IPageService pageService,
                                   IIamChainService iamChainService,
                                   IRecordService recordService,
                                   DatabaseSource database,
                                   long allocationUnitId)
    {
        Logger = logger;
        ColumnstoreService = columnstoreService;
        PageService = pageService;
        IamChainService = iamChainService;
        RecordService = recordService;
        Database = database;
        AllocationUnitId = allocationUnitId;

        Dock = BuildDock();
    }

    private ILogger<ColumnstoreTabViewModel> Logger { get; }

    internal ColumnstoreService ColumnstoreService { get; }

    internal IPageService PageService { get; }

    internal IIamChainService IamChainService { get; }

    internal IRecordService RecordService { get; }

    public DatabaseSource Database { get; }

    public long AllocationUnitId { get; }

    [ObservableProperty]
    private ColumnStoreIndex? _index;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isStructureLoading;

    private const int SpinnerDelayMs = 100;

    [ObservableProperty]
    private string _loadingText = "Loading columnstore index...";

    [ObservableProperty]
    private string _indexDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowGroupCountDescription))]
    private int _rowGroupCount;

    public string RowGroupCountDescription => RowGroupCount == 1 ? "1 row group" : $"{RowGroupCount} row groups";

    public ObservableCollection<RowGroupSummary> RowGroups { get; } = [];

    public ObservableCollection<SegmentSummary> Segments { get; } = [];

    public async Task Load()
    {
        IsLoading = true;
        IsInitialized = false;
        IsDrawingReady = false;

        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        try
        {
            var allocationUnit = Database.AllocationUnits.GetValueOrDefault(AllocationUnitId)
                                 ?? Database.AllocationUnits
                                            .Values
                                            .FirstOrDefault(a => a.AllocationUnitId == AllocationUnitId);

            if (allocationUnit is null)
            {
                LoadingText = $"Allocation unit {AllocationUnitId} was not found";

                return;
            }

            Name = string.IsNullOrEmpty(allocationUnit.IndexName)
                ? allocationUnit.TableName
                : allocationUnit.IndexName;

            var index = await Task.Run(() => ColumnstoreService.GetIndex(allocationUnit, Database, CancellationToken),
                                       CancellationToken);

            var summaries = await Task.Run(() => RowGroupSummary.Build(index), CancellationToken);

            await spinnerDelay.CancelAsync();

            Index = index;

            RowGroups.Clear();
            Segments.Clear();

            foreach (var summary in summaries)
            {
                RowGroups.Add(summary);

                foreach (var segment in summary.Segments)
                {
                    Segments.Add(segment);
                }
            }

            IndexTypeDescription = index.IsClustered ? "Clustered" : "Non Clustered";

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
            Logger.LogError(exception, "Failed to load columnstore index {AllocationUnitId}", AllocationUnitId);

            LoadingText = exception.Message;
        }
        finally
        {
            await spinnerDelay.CancelAsync();

            IsStructureLoading = false;

            IsLoading = false;
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

                var summary = Dictionaries.FirstOrDefault(d => d.ColumnId == dictionary.ColumnId
                                                               && d.DictionaryId == dictionary.DictionaryId);

                if (summary is not null)
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

    /// <summary>
    /// Whether the index is the table or an index over it, which decides whether it carries a row locator
    /// </summary>
    [ObservableProperty]
    private string _indexTypeDescription = string.Empty;

    public short DatabaseId => Database.DatabaseId;

    /// <summary>
    /// Every dictionary the index holds, global ones once and local ones per row group
    /// </summary>
    public ObservableCollection<DictionarySummary> Dictionaries { get; } = [];

    private void BuildDictionaries(ColumnStoreIndex index)
    {
        Dictionaries.Clear();

        foreach (var column in index.Columns)
        {
            if (column.GlobalDictionary is { } global)
            {
                Dictionaries.Add(new DictionarySummary { Dictionary = global, ColumnName = column.Name });
            }
        }

        foreach (var segment in index.RowGroups.SelectMany(r => r.Segments))
        {
            if (segment.LocalDictionary is { } local)
            {
                Dictionaries.Add(new DictionarySummary
                {
                    Dictionary = local,
                    ColumnName = segment.Column?.Name ?? $"Column {local.ColumnId}"
                });
            }
        }
    }

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
