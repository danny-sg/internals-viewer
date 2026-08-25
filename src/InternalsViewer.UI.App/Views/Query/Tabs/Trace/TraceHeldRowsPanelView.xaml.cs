using System.Collections.Specialized;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceHeldRowsPanelView : UserControl
{
    private TraceHeldRowsViewModel? _subscribed;

    public TraceHeldRowsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            Resubscribe();
        };
    }

    public TraceHeldRowsViewModel? ViewModel => DataContext as TraceHeldRowsViewModel;

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
