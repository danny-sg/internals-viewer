using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
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

public sealed partial class RecordGrid
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
            new PropertyMetadata(default, OnPropertyChanged));

    public RecordGrid()
    {
        InitializeComponent();
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.Property == RecordsProperty)
        {
            var control = (RecordGrid)d;

            control.AddColumns();
        }
    }

    private void AddColumns()
    {
        DataGrid.Columns.Clear();

        var slotColumn = new DataGridTextColumn
        {
            Binding = new Binding { Path = new PropertyPath("Slot") },
            Header = "Slot",
            ElementStyle = (Style)Resources["SlotCellStyle"],
        };

        DataGrid.Columns.Add(slotColumn);

        var converter = new RecordValueConverter();

        if (Records.Any())
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

        if (Records.Any(r => r.DownPagePointer != PageAddress.Empty))
        {
            var column = new PageAddressLinkButtonColumn<IndexRecordModel>
            {
                Binding = new Binding { Path = new PropertyPath("DownPagePointer") },
                Header = "Down Page Pointer",
                ElementStyle = (Style)Resources["PageAddressStyle"],
            };

            
            column.PageClicked += (sender, args) => PageClicked?.Invoke(this, args);

            DataGrid.Columns.Add(column);
        }

        if (Records.Any(r => r.RowIdentifier != null))
        {
            var column = new DataGridTextColumn
            {
                Binding = new Binding { Path = new PropertyPath("RowIdentifier") },
                Header = "RID",
                ElementStyle = (Style)Resources["PageAddressStyle"],
            };

            DataGrid.Columns.Add(column);
        }
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var grid = sender as DataGrid;

        if (grid?.SelectedItem is IndexRecordModel row)
        {
            var address = row.DownPagePointer;

            PageOver?.Invoke(this, new PageAddressEventArgs(address));
        }
    }
}

public class PageAddressLinkButtonColumn<T> : DataGridBoundColumn
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var pageAddress = PageAddress.Empty;

        if (Binding != null)
        {
            var value = Binding.Path.Path;
            var propertyValue = dataItem.GetType().GetProperty(value)?.GetValue(dataItem);

            pageAddress = (PageAddress)(propertyValue ?? pageAddress);
        }

        var button = new HyperlinkButton
        {
            Content = pageAddress.ToString(),
            Command = new RelayCommand(() =>
            {
                PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress));
            }),
        };

        return button;
    }

    protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
    {
        return null!;
    }
}