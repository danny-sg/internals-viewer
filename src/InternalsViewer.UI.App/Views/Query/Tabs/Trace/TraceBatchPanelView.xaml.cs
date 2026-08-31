using System.ComponentModel;
using InternalsViewer.UI.App.Controls.Trace.Batch;
using InternalsViewer.UI.App.Models.Query.Trace.Batch;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceBatchPanelView : UserControl
{
    private static SolidColorBrush PureBrush { get; } = new(Colors.SeaGreen);

    private static SolidColorBrush ImpureBrush { get; } = new(Colors.SlateGray);

    private static TraceBatchViewModel Empty { get; } = new();

    private int _columnVersion = -1;

    private object? _boundRows;

    public TraceBatchPanelView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Loaded += OnLoaded;

        Unloaded += OnUnloaded;
    }

    public TraceBatchViewModel Batch => DataContext as TraceBatchViewModel ?? Empty;

    public Brush PurityBrush => Batch.IsPure ? PureBrush : ImpureBrush;

    private TraceBatchViewModel? Attached { get; set; }

    public void CloseDetailPane() => Attached?.SelectSlot(null);

    public void CloseDeepPane()
    {
        DeepDataTable.SelectedItem = null;

        Attached?.SelectDeepData(null);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Reattach();

    private void OnLoaded(object sender, RoutedEventArgs e) => Reattach();

    /// <summary>
    /// Binds to the batch and takes its current state, which a tab switch has to redo
    /// </summary>
    private void Reattach()
    {
        Detach();

        Attach();

        Bindings.Update();

        _columnVersion = -1;

        _boundRows = null;

        RebuildColumns();

        Refresh();

        UpdateDetail();

        UpdateDeepDetail();
    }

    private void Attach()
    {
        if (DataContext is not TraceBatchViewModel batch)
        {
            return;
        }

        Attached = batch;

        batch.PropertyChanged += OnBatchPropertyChanged;

        batch.DeepDataRequested += OnDeepDataRequested;
    }

    private void Detach()
    {
        if (Attached is not { } batch)
        {
            return;
        }

        batch.PropertyChanged -= OnBatchPropertyChanged;

        batch.DeepDataRequested -= OnDeepDataRequested;

        Attached = null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void OnBatchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Bindings.Update();

        RebuildColumns();

        Refresh();

        UpdateDetail();

        UpdateDeepDetail();
    }

    private void OnDeepDataSelectionChanged(object sender, SelectionChangedEventArgs e)
        => Attached?.SelectDeepData(DeepDataTable.SelectedItem as BatchDeepDataRow);

    private void UpdateDeepDetail()
    {
        var isVisible = Attached?.SelectedDeepData is not null;

        DeepSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        DeepHeader.Visibility = DeepSplitter.Visibility;

        DeepDetailList.Visibility = DeepSplitter.Visibility;

        DeepSplitterRow.Height = isVisible ? GridLength.Auto : new GridLength(0);

        DeepHeaderRow.Height = DeepSplitterRow.Height;

        DeepDetailRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void RebuildColumns()
    {
        if (Attached is not { } batch || _columnVersion == batch.ColumnVersion)
        {
            return;
        }

        _columnVersion = batch.ColumnVersion;

        _boundRows = null;

        VectorTable.Columns.Clear();

        VectorTable.Columns.Add(new TableViewTextColumn
        {
            Header = "Index",
            Width = new GridLength(70),
            Binding = new Binding { Path = new PropertyPath("RowIndex") }
        });

        foreach (var column in batch.Columns)
        {
            VectorTable.Columns.Add(new BatchSlotColumn(column)
            {
                Header = column.Name,
                Width = new GridLength(170),
                SlotClicked = OnSlotClicked,
                DeepDataClicked = OnDeepDataClicked
            });
        }
    }

    private void Refresh()
    {
        if (Attached is not { } batch || ReferenceEquals(_boundRows, batch.Rows))
        {
            return;
        }

        _boundRows = batch.Rows;

        VectorTable.ItemsSource = batch.Rows;
    }

    private void UpdateDetail()
    {
        var isVisible = Attached?.SelectedSlot is not null;

        DetailSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        DetailHeader.Visibility = DetailSplitter.Visibility;

        DetailList.Visibility = DetailSplitter.Visibility;

        SplitterRow.Height = isVisible ? GridLength.Auto : new GridLength(0);

        DetailHeaderRow.Height = SplitterRow.Height;

        DetailRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void OnSlotClicked(BatchSlotSelection selection) => Attached?.SelectSlot(selection);

    private void OnDeepDataClicked(int index) => Attached?.RequestDeepData(index);

    private void OnDeepDataRequested(int index)
    {
        if (Attached is not { } batch || index < 0 || index >= batch.DeepData.Count)
        {
            return;
        }

        BatchTabs.SelectedIndex = 2;

        var target = batch.DeepData[index];

        DeepDataTable.SelectedItem = target;

        DispatcherQueue.TryEnqueue(() => DeepDataTable.ScrollIntoView(target));
    }
}
