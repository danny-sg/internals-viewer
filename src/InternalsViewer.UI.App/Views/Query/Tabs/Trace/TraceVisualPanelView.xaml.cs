using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceVisualPanelView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

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

    public Visibility IndexVisibility(TraceKind kind, bool isInitialized)
        => kind == TraceKind.Index && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility(TraceKind kind, bool isInitialized)
        => kind == TraceKind.Index && !isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AllocationVisibility(TraceKind kind, bool isInitialized)
        => kind == TraceKind.Allocation && isInitialized ? Visibility.Visible : Visibility.Collapsed;
}
