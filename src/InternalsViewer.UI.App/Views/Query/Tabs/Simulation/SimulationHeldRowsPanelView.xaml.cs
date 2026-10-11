using System.Collections.Specialized;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Simulation;

public sealed partial class SimulationHeldRowsPanelView : UserControl
{
    private SimulationHeldRowsViewModel? _subscribed;

    public SimulationHeldRowsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            Resubscribe();
        };
    }

    public SimulationHeldRowsViewModel? ViewModel => DataContext as SimulationHeldRowsViewModel;

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
        var count = ViewModel?.Rows.Count ?? 0;

        StatusText.Text = count == 1 ? "1 row" : $"{count:N0} rows";
    }
}
