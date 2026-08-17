using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Helpers.Converters;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class RecordGrid : IDisposable
{
    public AllocationLayerGridViewModel ViewModel { get; } = new();

    public event EventHandler<PageAddressEventArgs>? PageOver;

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public ObservableCollection<IndexRecordModel> Records
    {
        get => (ObservableCollection<IndexRecordModel>)GetValue(RecordsProperty);
        set => SetValue(RecordsProperty, value);
    }

    public static readonly DependencyProperty RecordsProperty
        = DependencyProperty.Register(nameof(Records),
            typeof(ObservableCollection<IndexRecordModel>),
            typeof(RecordGrid),
            new PropertyMetadata(null, OnPropertyChanged));

    public int? SelectedSlot
    {
        get => (int?)GetValue(SelectedSlotProperty);
        set => SetValue(SelectedSlotProperty, value);
    }

    public static readonly DependencyProperty SelectedSlotProperty
        = DependencyProperty.Register(nameof(SelectedSlot),
            typeof(int?),
            typeof(RecordGrid),
            new PropertyMetadata(null, OnPropertyChanged));

    public bool HideSlotColumn { get; set; }

    public bool AutoScrollToEnd { get; set; }

    private static readonly SolidColorBrush MatchedBackground
        = new(Windows.UI.Color.FromArgb(255, 223, 244, 223));

    private static readonly SolidColorBrush MatchedForeground
        = new(Windows.UI.Color.FromArgb(255, 11, 93, 11));

    public RecordGrid()
    {
        InitializeComponent();

        RecordTable.ContainerContentChanging += OnContainerContentChanging;
    }

    private static void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemContainer is not TableViewRow row)
        {
            return;
        }

        // Rows are recycled, so a row has to be reverted rather than left with the previous row's brushes
        if (args.Item is IndexRecordModel { IsMatched: true })
        {
            row.Background = MatchedBackground;
            row.Foreground = MatchedForeground;

            return;
        }

        row.ClearValue(BackgroundProperty);
        row.ClearValue(ForegroundProperty);
    }

    private ObservableCollection<IndexRecordModel>? _subscribedRecords;

    private bool _hasFieldColumns;

    private bool _isRebindQueued;

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RecordGrid)d;

        if (e.Property == RecordsProperty)
        {
            control.SubscribeRecords();
            control.Rebind();
        }

        control.DispatcherQueue.TryEnqueue(control.ApplySelectedSlot);
    }

    private void SubscribeRecords()
    {
        _subscribedRecords?.CollectionChanged -= OnRecordsCollectionChanged;

        _subscribedRecords = Records;

        _subscribedRecords?.CollectionChanged += OnRecordsCollectionChanged;
    }

    private void Rebind()
    {
        _isRebindQueued = false;

        RecordTable.ItemsSource = null;

        AddColumns();

        RecordTable.ItemsSource = Records;
    }

    private void OnRecordsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (AutoScrollToEnd && Records is { Count: > 0 })
        {
            RequestScrollToEnd();
        }

        if (_hasFieldColumns || _isRebindQueued || Records is not { Count: > 0 })
        {
            return;
        }

        _isRebindQueued = true;

        DispatcherQueue.TryEnqueue(Rebind);
    }

    private const int ScrollIntervalMs = 150;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _scrollTimer;

    private long _lastScroll;

    private void RequestScrollToEnd()
    {
        var now = Environment.TickCount64;

        if (now - _lastScroll >= ScrollIntervalMs)
        {
            _lastScroll = now;

            ScrollToEnd();

            return;
        }

        if (_scrollTimer is null)
        {
            _scrollTimer = DispatcherQueue.CreateTimer();

            _scrollTimer.Interval = TimeSpan.FromMilliseconds(ScrollIntervalMs);
            _scrollTimer.IsRepeating = false;

            _scrollTimer.Tick += (_, _) =>
            {
                _lastScroll = Environment.TickCount64;

                ScrollToEnd();
            };
        }

        if (!_scrollTimer.IsRunning)
        {
            _scrollTimer.Start();
        }
    }

    private void ScrollToEnd()
    {
        if (Records is { Count: > 0 } records)
        {
            RecordTable.ScrollIntoView(records[^1]);
        }
    }

    private void ApplySelectedSlot()
    {
        var record = SelectedSlot is null
            ? null
            : Records?.FirstOrDefault(r => r.Slot == SelectedSlot);

        RecordTable.SelectedItem = record;

        if (record is not null)
        {
            RecordTable.ScrollIntoView(record);
        }
    }

    private void AddColumns()
    {
        RemoveEventHandlers();

        RecordTable.Columns.Clear();

        _hasFieldColumns = Records?.Count > 0;

        if (!HideSlotColumn)
        {
            var slotColumn = new TableViewTextColumn
            {
                Binding = new Binding { Path = new PropertyPath("Slot") },
                Header = "Slot",
                Width = new GridLength(60),
                ElementStyle = (Style) Resources["SlotCellStyle"],
            };

            RecordTable.Columns.Add(slotColumn);
        }

        var converter = new RecordValueConverter();

        if (Records?.Any() == true)
        {
            var record = Records.First();

            // Bound by position, because a row a join produced can carry the same column name more than once
            for (var index = 0; index < record.Fields.Count; index++)
            {
                var column = new TableViewTextColumn
                {
                    Binding = new Binding { Converter = converter, ConverterParameter = index },
                    Header = record.Fields[index].Name,
                    Width = GetColumnWidth(record.Fields[index]),
                    CanSort = false
                };

                RecordTable.Columns.Add(column);
            }
        }

        if (Records?.Any(r => r.DownPagePointer != PageAddress.Empty) == true)
        {
            var column = new PageAddressLinkButtonColumn<IndexRecordModel>
            {
                Binding = new Binding { Path = new PropertyPath("DownPagePointer") },
                Header = "Down Page Pointer",
                Width = new GridLength(150)
            };

            column.PageClicked += OnPageClicked;
            column.PageOver += OnPageOver;

            RecordTable.Columns.Add(column);
        }

        // A nonclustered index of a heap stores the row identifier as a hidden column, so it is already among the fields
        var hasRidField = RecordTable.Columns.Any(c => c.Header as string == "RID");

        if (!hasRidField && Records?.Any(r => r.RowIdentifier != null && r.RowIdentifier != RowIdentifier.Empty) == true)
        {
            var column = new TableViewTextColumn
            {
                Binding = new Binding { Path = new PropertyPath("RowIdentifier") },
                Header = "RID",
                Width = new GridLength(140)
            };

            RecordTable.Columns.Add(column);
        }
    }

    /// <summary>
    /// The width a column is fixed at, from what the field holds and how long its name is
    /// </summary>
    /// <remarks>
    /// A column left to size itself is measured against every row as it is realised, and one row wider than the rest widens the column and
    /// sends the whole grid back through layout. A trace adds rows a step at a time, so that measure is paid over and over.
    /// </remarks>
    private static GridLength GetColumnWidth(IndexRecordFieldModel field)
    {
        var width = field.DataType switch
        {
            SqlDbType.Bit or SqlDbType.TinyInt or SqlDbType.SmallInt => 60,
            SqlDbType.Int or SqlDbType.BigInt => 90,
            SqlDbType.Real or SqlDbType.Float or SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney => 100,
            SqlDbType.Date or SqlDbType.Time or SqlDbType.SmallDateTime => 120,
            SqlDbType.DateTime or SqlDbType.DateTime2 or SqlDbType.DateTimeOffset => 160,
            SqlDbType.UniqueIdentifier => 240,
            _ => 140
        };

        return new GridLength(Math.Max(width, field.Name.Length * 7 + 24));
    }

    /// <summary>
    /// Cleans up the event handlers as they seem to be compound on each refresh
    /// </summary>
    private void RemoveEventHandlers()
    {
        foreach (var column in RecordTable.Columns)
        {
            if (column is PageAddressLinkButtonColumn<IndexRecordModel> linkButtonColumn)
            {
                linkButtonColumn.PageClicked -= OnPageClicked;
                linkButtonColumn.PageOver -= OnPageOver;
            }
        }
    }

    private void OnPageClicked(object? sender, PageAddressEventArgs e)
    {
        PageClicked?.Invoke(this, e);
    }

    private void OnPageOver(object? sender, PageAddressEventArgs e)
    {
        PageOver?.Invoke(this, e);
    }

    public void Dispose()
    {
        RecordTable.ContainerContentChanging -= OnContainerContentChanging;

        RemoveEventHandlers();

        _scrollTimer?.Stop();
        _scrollTimer = null;

        _subscribedRecords?.CollectionChanged -= OnRecordsCollectionChanged;
        _subscribedRecords = null;
    }
}