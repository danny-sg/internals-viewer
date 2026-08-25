using System;
using System.Collections.Generic;
using System.Data;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Controls.Columnstore;
using Microsoft.Extensions.Logging;
using InternalsViewer.UI.App.Services.Diagnostics;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Segment;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Segment;

/// <summary>
/// One column segment, its blob broken into the regions the structure table navigates
/// </summary>
public sealed partial class SegmentTabViewModel(ILogger<SegmentTabViewModel> logger,
                                                ColumnstoreService columnstoreService,
                                                DatabaseSource database,
                                                SegmentSummary segment,
                                                Action<SegmentSummary>? openDictionary = null) : ObservableObject, IDisposable
{
    /// <summary>
    /// Opens the dictionary the segment reads, which only the dock above this tab knows how to do
    /// </summary>
    private Action<SegmentSummary>? OpenDictionaryAction { get; } = openDictionary;

    public bool HasDictionary => Segment.HasDictionary;

    /// <summary>
    /// Whether the dictionary is the column's own or one built for this row group, as a chip beside the button
    /// </summary>
    public IReadOnlyList<SegmentBadge> DictionaryBadges => Segment.Dictionary is not { } dictionary
        ? []
        : [SegmentBadge.Create(Segment.DictionaryScope, ColumnstoreLayout.GetDictionaryColour(dictionary.Type))];

    public void OpenDictionary() => OpenDictionaryAction?.Invoke(Segment);

    public string GetCsIndexCommand(int printMode)
        => CsIndexCommand.Build(Segment, Database.DatabaseId, Segment.Segment.Key.HobtId, printMode);

    private ILogger<SegmentTabViewModel> Logger { get; } = logger;

    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentSummary Segment { get; } = segment;

    /// <summary>
    /// The type the column was declared as, which the header shows beside its name
    /// </summary>
    /// <remarks>
    /// Surfaced a field at a time because a data type is drawn from four of them, and x:Bind cannot reach through a
    /// structure that may not be there.
    /// </remarks>
    public SqlDbType? DataType => Segment.Structure?.DataType;

    public int Precision => Segment.Structure?.Precision ?? 0;

    public int Scale => Segment.Structure?.Scale ?? 0;

    public int DataLength => Segment.Structure?.DataLength ?? 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBookmarks))]
    [NotifyPropertyChangedFor(nameof(Bookmarks))]
    [NotifyPropertyChangedFor(nameof(BitpackUnits))]
    [NotifyPropertyChangedFor(nameof(HasRleArray))]
    [NotifyPropertyChangedFor(nameof(RleRuns))]
    [NotifyPropertyChangedFor(nameof(RleValueLabel))]
    [NotifyPropertyChangedFor(nameof(HexAreas))]
    [NotifyPropertyChangedFor(nameof(RleIndexLabel))]
    [NotifyPropertyChangedFor(nameof(HasBitpackArray))]
    [NotifyPropertyChangedFor(nameof(HasVariableLengthData))]
    [NotifyPropertyChangedFor(nameof(StorageBadges))]
    private SegmentBlob? _blob;

    partial void OnBlobChanged(SegmentBlob? value)
    {
        _rleRuns = null;
        _bookmarks = null;
        _bitpackUnits = null;
        _hexAreas = null;
        _dataIdStream = null;
    }

    public IReadOnlyList<SegmentBadge> EncodingBadges =>
    [
        SegmentBadge.Create(Segment.EncodingDescription, ColumnstoreLayout.GetEncodingColour(Segment.Encoding))
    ];

    public IReadOnlyList<SegmentBadge> StorageBadges
    {
        get
        {
            var storage = SegmentStorageExtensions.Classify(Blob?.Header);

            return storage == SegmentStorage.Unknown
                ? []
                : [SegmentBadge.Create(storage.Describe(), ColumnstoreLayout.GetStorageColour(storage))];
        }
    }

    private void GoToOffset(int offset) => Hex.GoToOffset(offset);

    private SegmentValueDecoder? Decoder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoaded;

    /// <summary>
    /// Whether the tabs are being laid out before they are shown, which the reader waits through as part of loading
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isPreparing;

    public bool IsBusy => !IsLoaded || IsPreparing;

    private string? _statusBeforePreparing;

    partial void OnIsPreparingChanged(bool value)
    {
        if (value)
        {
            _statusBeforePreparing = StatusText;

            StatusText = "Loading...";

            return;
        }

        if (_statusBeforePreparing is { } previous)
        {
            StatusText = previous;
        }
    }

    [ObservableProperty]
    private bool _isSegmentLoading;

    private const int SpinnerDelayMs = 100;

    [ObservableProperty]
    private string _statusText = "Loading Segment...";

    /// <summary>
    /// The whole segment blob, the regions being ranges within it rather than blobs of their own
    /// </summary>
    public BlobHexViewModel Hex { get; } = new();

    public ObservableCollection<SegmentElement> Elements { get; } = [];

    /// <summary>
    /// Whether the grids show the working behind a value, or only the value itself
    /// </summary>
    /// <remarks>
    /// The flag reaches a cell through the row it is bound to, so the rows are rebuilt to take a change. Both lists
    /// are cheap to rebuild, one being an index over the segment and the other a single packed unit.
    /// </remarks>
    [ObservableProperty]
    private bool _isDerivationVisible = true;

    partial void OnIsDerivationVisibleChanged(bool value)
    {
        if (Blob is { } blob)
        {
            BuildRows(blob);
        }

        BitpackUnit = GetBitpackUnit(Hex.SelectedMarker);
    }

    /// <summary>
    /// The working behind a row's value, which a wide store answers by ordinal rather than by data id
    /// </summary>
    private ValueDerivation? DeriveValueTimed(int ordinal, long dataId)
    {
        using var timing = Logger.Time("Derive row value", $"row {ordinal}");

        return DeriveValue(ordinal, dataId);
    }

    private ValueDerivation? DeriveValue(int ordinal, long dataId)
    {
        if (Blob?.VariableLengthData is { IsWide: true } store)
        {
            return SegmentValueDerivation.BuildWide(Segment.Segment, store, ordinal);
        }

        return DeriveDataIdValue(dataId);
    }

    private ValueDerivation? DeriveDataIdValue(long dataId)
        => Decoder is { } decoder ? SegmentValueDerivation.Build(Segment.Segment, decoder, dataId) : null;

    public async Task Load(CancellationToken cancellationToken)
    {
        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        try
        {
            var blob = await Task.Run(
                () => ColumnstoreService.GetSegmentBlob(Database, Segment.Segment, cancellationToken, isMarkEnabled: true),
                cancellationToken);

            var decoder = await Task.Run(
                () => ColumnstoreService.GetSegmentDecoder(Database, Segment.Segment, cancellationToken),
                cancellationToken);

            await spinnerDelay.CancelAsync();

            Decoder = decoder;

            Blob = blob;

            Hex.WindowMoved -= OnWindowMoved;
            Hex.PropertyChanged -= OnHexPropertyChanged;

            Hex.MarkerFactory = (start, length) => BuildMarkers(blob, start, length);

            Hex.WindowMoved += OnWindowMoved;
            Hex.PropertyChanged += OnHexPropertyChanged;

            Hex.SetData(blob.Data);

            Elements.Clear();

            foreach (var element in SegmentElementBuilder.Build(blob))
            {
                Elements.Add(element);
            }

            BuildRows(blob);

            BuildValuePages(blob);

            Region = SegmentRegion.Header;

            SelectedRegionTabIndex = 0;

            SelectedVariableLengthDataTabIndex = 0;

            GoToRegion(SegmentRegion.Header);

            StatusText = $"{blob.Data.Length} bytes";

            IsLoaded = true;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
        finally
        {
            await spinnerDelay.CancelAsync();

            IsSegmentLoading = false;
        }
    }

    /// <summary>
    /// Holds the spinner back so a segment that reads straight away does not flash one up
    /// </summary>
    private async Task ShowSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsSegmentLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // The segment read finished inside the delay, so no spinner is wanted
        }
    }

    public void Dispose()
    {
        Hex.WindowMoved -= OnWindowMoved;

        Hex.PropertyChanged -= OnHexPropertyChanged;

        Hex.Dispose();

        PayloadHex.Dispose();
    }
}
