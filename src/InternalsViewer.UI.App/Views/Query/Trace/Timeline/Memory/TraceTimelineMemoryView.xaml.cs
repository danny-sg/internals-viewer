using System.ComponentModel;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class TraceTimelineMemoryView : UserControl
{
    private const double DetailsMinimumWidth = 240;

    public TraceTimelineMemoryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    public MemoryViewMode Mode { get; private set; } = MemoryViewMode.Compact;

    private PlayheadMemoryViewModel? Subscribed { get; set; }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs e)
    {
        if (Subscribed is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Subscribed = ViewModel;

        if (Subscribed is { } current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
        }

        Bindings.Update();

        ApplyLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayheadMemoryViewModel.IsHexVisible))
        {
            ApplyLayout();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Mode = e.NewSize.Width >= HexViewControl.StandardWidth + DetailsMinimumWidth ? MemoryViewMode.Full : MemoryViewMode.Compact;

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var isHexVisible = ViewModel?.IsHexVisible != false;

        var isDetailsVisible = !isHexVisible || Mode == MemoryViewMode.Full;

        HexColumn.Width = !isHexVisible
            ? new GridLength(0)
            : isDetailsVisible
                ? new GridLength(HexViewControl.StandardWidth)
                : new GridLength(1, GridUnitType.Star);

        DetailsColumn.Width = isDetailsVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Details.Visibility = isDetailsVisible ? Visibility.Visible : Visibility.Collapsed;

        Hex.Visibility = isHexVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnChangeNavigationRequested(object? sender, HexChangeNavigation e) => ViewModel?.GoToChange(e.Offset, e.IsForward);

    private void OnOffsetClicked(object? sender, int offset) => ViewModel?.ShowAccessesAt(offset);

    private void OnHashTableClick(object sender, RoutedEventArgs e) => ViewModel?.OpenHashTable();

    private void OnSortTableClick(object sender, RoutedEventArgs e) => ViewModel?.OpenSortTable();
}
