using System.Collections.Specialized;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceRowStreamPanelView : UserControl
{
    public TraceRowStreamViewModel? ViewModel => DataContext as TraceRowStreamViewModel;

    private TraceRowStreamViewModel? _subscribed;

    public TraceRowStreamPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            Resubscribe();
        };
    }

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
