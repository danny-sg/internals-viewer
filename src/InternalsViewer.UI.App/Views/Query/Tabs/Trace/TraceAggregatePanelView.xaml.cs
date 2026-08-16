using System.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceAggregatePanelView : UserControl
{
    public TraceAggregateViewModel? ViewModel => DataContext as TraceAggregateViewModel;

    private TraceAggregateViewModel? _subscribed;

    public TraceAggregatePanelView()
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
            _subscribed.PropertyChanged -= OnViewModelChanged;
        }

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged += OnViewModelChanged;
        }

        UpdateHeader();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => UpdateHeader();

    private void UpdateHeader()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        GroupHeadingText.Text = viewModel.GroupHeading;

        GroupKeyText.Text = viewModel.GroupKey;
        GroupKeyText.Visibility = viewModel.GroupKey.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        StatusText.Text = viewModel.IsGrouped
            ? $"{viewModel.GroupRows:N0} rows, {viewModel.Groups:N0} groups"
            : $"{viewModel.GroupRows:N0} rows";
    }
}
