using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBatchPartPanelView : UserControl
{
    public MemoryBatchPartPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnBatchClick(object sender, RoutedEventArgs e) => ViewModel?.Batch.OpenBatch();

    private void OnNextPageClick(object sender, RoutedEventArgs e) => ViewModel?.Batch.OpenNextPage();

    private void OnHeaderMenuOpening(object? sender, ResultHeaderMenuEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            VectorTypeMenu.Add(e.Menu, viewModel.Batch, e.Column.Ordinal, isVectorsTable: false);
        }
    }

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            VectorTypeMenu.Add(e.Menu, viewModel.Batch, e.Ordinal, isVectorsTable: false);
        }
    }
}
