using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceVisualPanelView : UserControl
{
    public TraceVisualViewModel? ViewModel => DataContext as TraceVisualViewModel;

    public TraceVisualPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            UpdateStackVisibility();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStackVisibility();

        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadVisualAsync();
        }
    }

    private void UpdateStackVisibility()
    {
        var isVisible = ViewModel?.IsSideStackVisible == true;

        StackSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        StackGrid.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        SplitterRow.Height = isVisible ? GridLength.Auto : new GridLength(0);
        StackRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
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
