using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Helpers.Converters;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

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
            typeof(ObservableCollection<AllocationLayer>),
            typeof(AllocationLayerGrid),
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

    public static readonly DependencyProperty HideSlotColumnProperty
        = DependencyProperty.Register(nameof(HideSlotColumn),
            typeof(bool),
            typeof(RecordGrid),
            new PropertyMetadata(false, OnPropertyChanged));

    public RecordGrid()
    {
        InitializeComponent();
    }

    private ObservableCollection<IndexRecordModel>? _subscribedRecords;

    private bool _hasFieldColumns;

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RecordGrid)d;

        if (e.Property == RecordsProperty)
        {
            control.SubscribeRecords();
            control.AddColumns();
        }

        control.DispatcherQueue.TryEnqueue(control.ApplySelectedSlot);
    }

    private void SubscribeRecords()
    {
        if (_subscribedRecords is not null)
        {
            _subscribedRecords.CollectionChanged -= OnRecordsCollectionChanged;
        }

        _subscribedRecords = Records;

        if (_subscribedRecords is not null)
        {
            _subscribedRecords.CollectionChanged += OnRecordsCollectionChanged;
        }
    }

    private void OnRecordsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!_hasFieldColumns && Records?.Count > 0)
        {
            AddColumns();
        }
    }

    private void ApplySelectedSlot()
    {
        var record = SelectedSlot is null
            ? null
            : Records?.FirstOrDefault(r => r.Slot == SelectedSlot);

        DataGrid.SelectedItem = record;

        if (record is not null)
        {
            DataGrid.ScrollIntoView(record, null);
        }
    }

    private void AddColumns()
    {
        RemoveEventHandlers();

        DataGrid.Columns.Clear();

        _hasFieldColumns = Records?.Count > 0;

        if (!HideSlotColumn)
        {
            var slotColumn = new DataGridTextColumn
            {
                Binding = new Binding { Path = new PropertyPath("Slot") },
                Header = "Slot",
                ElementStyle = (Style) Resources["SlotCellStyle"],
            };

            DataGrid.Columns.Add(slotColumn);
        }

        var converter = new RecordValueConverter();

        if (Records?.Any() == true)
        {
            var record = Records.First();

            foreach (var t in record.Fields)
            {
                var column = new DataGridTextColumn
                {
                    Binding = new Binding { Converter = converter, ConverterParameter = t.Name },
                    Header = t.Name,
                };

                DataGrid.Columns.Add(column);
            }
        }

        if (Records?.Any(r => r.DownPagePointer != PageAddress.Empty) == true)
        {
            var column = new PageAddressLinkButtonColumn<IndexRecordModel>
            {
                Binding = new Binding { Path = new PropertyPath("DownPagePointer") },
                Header = "Down Page Pointer"
            };

            column.PageClicked += OnPageClicked;
            column.PageOver += OnPageOver;

            DataGrid.Columns.Add(column);
        }

        if (Records?.Any(r => r.RowIdentifier != null && r.RowIdentifier != RowIdentifier.Empty) == true)
        {
            var column = new DataGridTextColumn
            {
                Binding = new Binding { Path = new PropertyPath("RowIdentifier") },
                Header = "RID"
            };

            DataGrid.Columns.Add(column);
        }
    }

    /// <summary>
    /// Cleans up the event handlers as they seem to be compound on each refresh
    /// </summary>
    private void RemoveEventHandlers()
    {
        foreach (var column in DataGrid.Columns)
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
        RemoveEventHandlers();

        if (_subscribedRecords is not null)
        {
            _subscribedRecords.CollectionChanged -= OnRecordsCollectionChanged;
            _subscribedRecords = null;
        }
    }
}