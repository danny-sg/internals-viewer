using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHistoryPanelView : UserControl
{
    public MemoryHistoryPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnClearClick(object sender, RoutedEventArgs e) => ViewModel?.Writes.ClearFilters();

    private void OnSortRequested(object? sender, ResultSortEventArgs e) => ViewModel?.Writes.Sort(e.Ordinal, e.Direction);

    private void OnCallClicked(object? sender, CallValue value) => ViewModel?.History.ShowCall(value);

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var index = (int)e.Row.Id;

        e.Menu.Items.Add(MenuItems.Create("Filter To Function", () => viewModel.Writes.FilterToFunction(index)));

        e.Menu.Items.Add(MenuItems.Create("Filter To Address", () => viewModel.Writes.FilterToAddress(index)));
    }

    private void OnCellHovered(object? sender, ResultCellHoverEventArgs e)
    {
        if (ViewModel is { } viewModel && e.Ordinal == MemoryWritesViewModel.PositionColumn)
        {
            viewModel.MarkWrite(e.IsOver ? (int)e.Row.Id : null);
        }
    }
}
