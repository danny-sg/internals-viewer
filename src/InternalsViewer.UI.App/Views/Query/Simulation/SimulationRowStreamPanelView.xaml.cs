using System.Collections.Specialized;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationRowStreamPanelView : UserControl
{
    private SimulationRowStreamViewModel? _subscribed;

    public SimulationRowStreamPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            Resubscribe();
        };
    }

    public SimulationRowStreamViewModel? ViewModel => DataContext as SimulationRowStreamViewModel;

    private void Resubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.Rows.CollectionChanged -= OnRowsChanged;
        }

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.Rows.CollectionChanged += OnRowsChanged;
        }

        UpdateStatus();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        if (ViewModel is not { IsAccumulating: true } viewModel)
        {
            StatusFooter.Visibility = Visibility.Collapsed;

            return;
        }

        StatusFooter.Visibility = Visibility.Visible;

        StatusText.Text = viewModel.Rows.Count == 1 ? "1 row" : $"{viewModel.Rows.Count:N0} rows";
    }
}
