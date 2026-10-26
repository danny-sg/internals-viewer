using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBatchPanelView : UserControl
{
    public MemoryBatchPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnHeaderLinkClicked(object? sender, ResultColumn e) => ViewModel?.Batch.OpenVector(e.Ordinal);

    private void OnHeaderMenuOpening(object? sender, ResultHeaderMenuEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            VectorTypeMenu.Add(e.Menu, viewModel.Batch, e.Column.Ordinal, isVectorsTable: true);
        }
    }

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            VectorTypeMenu.Add(e.Menu, viewModel.Batch, e.Ordinal, isVectorsTable: true);
        }
    }
}
