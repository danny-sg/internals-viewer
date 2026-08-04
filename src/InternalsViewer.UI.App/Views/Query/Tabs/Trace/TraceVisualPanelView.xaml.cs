using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceVisualPanelView : UserControl
{
    public TraceVisualViewModel? ViewModel => DataContext as TraceVisualViewModel;

    public TraceVisualPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadVisualAsync();
        }
    }

    public double DimOpacity(bool isDimmed)
        => isDimmed ? 0.35 : 1.0;

    public Windows.UI.Color ToWindowsColor(System.Drawing.Color colour)
        => Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

    public Visibility IndexVisibility(TraceVisualKind kind, bool isInitialized)
        => kind == TraceVisualKind.Index && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility(TraceVisualKind kind, bool isInitialized)
        => kind == TraceVisualKind.Index && !isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AllocationVisibility(TraceVisualKind kind, bool isInitialized)
        => kind == TraceVisualKind.Allocation && isInitialized ? Visibility.Visible : Visibility.Collapsed;
}
