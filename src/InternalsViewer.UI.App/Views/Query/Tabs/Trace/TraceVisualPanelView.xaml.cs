using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceVisualPanelView : UserControl
{
    public TraceVisualPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public TraceVisualViewModel? ViewModel => DataContext as TraceVisualViewModel;

    public Windows.UI.Color ToWindowsColor(System.Drawing.Color colour)
        => Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

    public Visibility IndexVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Index && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Index && !isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AllocationVisibility(TraceVisualType visualType, bool isInitialized)
        => visualType == TraceVisualType.Allocation && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadVisualAsync();
        }
    }
}
