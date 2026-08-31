using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceVisualPanelView : UserControl
{
    private const float IndexZoomToPageZoom = 1f;

    private const double AllocationZoomToPageZoom = 3d;

    public TraceVisualPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public TraceVisualViewModel? ViewModel => DataContext as TraceVisualViewModel;

    public Windows.UI.Color ToWindowsColor(System.Drawing.Color colour)
        => Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

    /// <summary>
    /// The zoom the index map animates to while Zoom to Page is on, or null to leave the zoom alone
    /// </summary>
    public float? ZoomToPageTarget(bool isZoomToPage) => isZoomToPage ? IndexZoomToPageZoom : null;

    /// <summary>
    /// The zoom the allocation map follows the current span at while Zoom to Page is on, or null to leave the zoom alone
    /// </summary>
    public double? AllocationZoomToPageTarget(bool isZoomToPage) => isZoomToPage ? AllocationZoomToPageZoom : null;

    public Visibility IndexVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Index && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType is TraceVisualType.Index or TraceVisualType.Columnstore && !isInitialized
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AllocationVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Allocation && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColumnstoreVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Columnstore && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadVisualAsync();
        }
    }
}
