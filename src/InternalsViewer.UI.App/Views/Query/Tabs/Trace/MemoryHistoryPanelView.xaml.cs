using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class MemoryHistoryPanelView : UserControl
{
    public MemoryHistoryPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnClearClick(object sender, RoutedEventArgs e) => ViewModel?.Writes.ClearFilters();

    private void OnCellHovered(object? sender, ResultCellHoverEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        switch (e.Ordinal)
        {
            case MemoryWritesViewModel.PositionColumn:
                viewModel.MarkWrite(e.IsOver && e.Row.Id is { } index ? (int)index : null);
                break;

            case MemoryWritesViewModel.FunctionColumn when e.Value is SwatchText function:
                viewModel.HighlightWriter(e.IsOver ? function.Text : null);
                break;
        }
    }
}
