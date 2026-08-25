using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Dictionary;

/// <summary>
/// Columnstore Dictionary Tab View Model
/// </summary>
public sealed partial class DictionaryTabViewModel(ColumnstoreService columnstoreService,
                                                   DatabaseSource database,
                                                   SegmentDictionary dictionary,
                                                   ColumnStoreColumn? column) : ObservableObject, IDisposable
{
    private const int SpinnerDelayMs = 100;

    private const float ShadeFactor = 0.72f;

    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentDictionary Dictionary { get; } = dictionary;

    public ColumnStoreColumn? Column { get; } = column;

    public string ColumnName => Column?.Name ?? $"Column {Dictionary.ColumnId}";

    public SqlDbType? DataType => Column?.Structure?.DataType;

    public int Precision => Column?.Structure?.Precision ?? 0;

    public int Scale => Column?.Structure?.Scale ?? 0;

    public int DataLength => Column?.Structure?.DataLength ?? 0;

    public IReadOnlyList<SegmentBadge> ScopeBadges =>
    [
        Dictionary.IsGlobal
            ? SegmentBadge.Create("Global", ColumnstoreColours.GlobalScope)
            : SegmentBadge.Create($"Local {Dictionary.DictionaryId}", ColumnstoreColours.LocalScope)
    ];

    public IReadOnlyList<SegmentBadge> TypeBadges
    {
        get
        {
            var colour = ColumnstoreLayout.GetDictionaryColour(Dictionary.Type);

            var badges = new List<SegmentBadge>
            {
                SegmentBadge.Create($"{ColumnstoreLayout.GetDictionaryTypeDescription(Dictionary.Type)} Dictionary",
                                    colour)
            };

            if (StoreSubLobType is { } store)
            {
                badges.Add(SegmentBadge.Create(store.ToString().SplitCamelCase(),
                                               ColumnstoreColours.Shade(colour, ShadeFactor)));
            }

            return SegmentBadge.Compound(badges);
        }
    }

    private SubLobType? StoreSubLobType => Blob switch
    {
        NumericDictionary numeric => numeric.HashTable.SubLobType,
        StringDictionary strings => strings.Store.SubLobType,
        _ => null
    };

    public IReadOnlyList<SegmentBadge> FlagBadges => SegmentBadge.Compound([.. BuildFlagBadges()]);

    private IEnumerable<SegmentBadge> BuildFlagBadges()
    {
        if (Blob is not StringDictionary strings || strings.Pages.Length == 0)
        {
            yield break;
        }

        var huffman = strings.Pages.Count(p => p is HuffmanStringPage);

        yield return huffman switch
        {
            0 => SegmentBadge.Create("Uncompressed", ColumnstoreColours.UncompressedFlag),
            _ when huffman == strings.Pages.Length => SegmentBadge.Create("Huffman", ColumnstoreColours.HuffmanFlag),
            _ => SegmentBadge.Create($"Huffman {huffman}/{strings.Pages.Length}", ColumnstoreColours.HuffmanFlag)
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlagBadges))]
    [NotifyPropertyChangedFor(nameof(TypeBadges))]
    [NotifyPropertyChangedFor(nameof(HasPages))]
    [NotifyPropertyChangedFor(nameof(HasValues))]
    [NotifyPropertyChangedFor(nameof(HasHandles))]
    [NotifyPropertyChangedFor(nameof(Handles))]
    [NotifyPropertyChangedFor(nameof(EntryValueItemType))]
    [NotifyPropertyChangedFor(nameof(EntryLengthItemType))]
    private DictionaryBlob? _blob;

    /// <summary>
    /// The whole dictionary blob, a page being a range within it rather than a blob of its own
    /// </summary>
    public BlobHexViewModel Hex { get; } = new();

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isDictionaryLoading;

    [ObservableProperty]
    private string _statusText = "Loading Dictionary...";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private DictionaryEntryList? _entries;

    private bool _isLoading;

    [ObservableProperty]
    private bool _isDerivationVisible = true;

    partial void OnIsDerivationVisibleChanged(bool value)
    {
        if (Blob is { } blob)
        {
            Entries = new DictionaryEntryList(blob, value);
        }
    }

    public string GetCsIndexCommand(int printMode)
        => CsIndexCommand.Build(Dictionary, Database.DatabaseId, Dictionary.HobtId, printMode);

    public async Task Load(CancellationToken cancellationToken)
    {
        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        _isLoading = true;

        try
        {
            var blob = await Task.Run(
                () => ColumnstoreService.GetDictionaryBlob(Database, Dictionary, cancellationToken, isMarkEnabled: true),
                cancellationToken);

            await spinnerDelay.CancelAsync();

            Blob = blob;

            Hex.WindowMoved -= OnWindowMoved;

            Hex.WindowMoved += OnWindowMoved;

            Hex.PropertyChanged -= OnHexPropertyChanged;

            Hex.PropertyChanged += OnHexPropertyChanged;

            Hex.MarkerFactory = (start, length) => BuildMarkers(blob, start, length);

            Hex.SetData(blob.Data);

            Entries = new DictionaryEntryList(blob, IsDerivationVisible);

            Pages.Clear();

            if (blob is StringDictionary strings)
            {
                for (var i = 0; i < strings.Pages.Length; i++)
                {
                    Pages.Add(new DictionaryPageSummary { Index = i, Page = strings.Pages[i] });
                }

                SelectedPage = Pages.FirstOrDefault(p => p.Huffman is not null) ?? Pages.FirstOrDefault();
            }

            SummaryText = Describe(blob);

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

            IsDictionaryLoading = false;

            _isLoading = false;
        }
    }

    public void GoToDataId(long dataId)
    {
        if (Blob is not { } blob)
        {
            return;
        }

        var index = (int)(dataId - blob.FirstId);

        SelectedEntryIndex = index >= 0 && index < blob.EntryCount ? index : -1;
    }

    [ObservableProperty]
    private int _selectedEntryIndex = -1;

    private static string Describe(DictionaryBlob blob)
    {
        var pages = blob is StringDictionary strings ? $", {strings.Pages.Length} pages" : string.Empty;

        return $"{blob.EntryCount} entries, Data Id {blob.FirstId} to {blob.FirstId + blob.EntryCount - 1}"
               + $"{pages}, {blob.Data.Length} bytes";
    }

    private async Task ShowSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsDictionaryLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // The dictionary read finished inside the delay, so no spinner is wanted
        }
    }

    public void Dispose()
    {
        Hex.WindowMoved -= OnWindowMoved;

        Hex.PropertyChanged -= OnHexPropertyChanged;

        Hex.Dispose();
    }
}
