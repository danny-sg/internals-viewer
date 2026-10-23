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

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    public MemoryViewMode Mode { get; private set; } = MemoryViewMode.Compact;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var mode = e.NewSize.Width >= HexViewControl.StandardWidth + DetailsMinimumWidth ? MemoryViewMode.Full : MemoryViewMode.Compact;

        if (mode == Mode)
        {
            return;
        }

        Mode = mode;

        var isFull = mode == MemoryViewMode.Full;

        HexColumn.Width = isFull ? new GridLength(HexViewControl.StandardWidth) : new GridLength(1, GridUnitType.Star);

        DetailsColumn.Width = isFull ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Details.Visibility = isFull ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnChangeNavigationRequested(object? sender, HexChangeNavigation e) => ViewModel?.GoToChange(e.Offset, e.IsForward);

    private void OnOffsetClicked(object? sender, int offset) => ViewModel?.ShowHistoryAt(offset);
}
