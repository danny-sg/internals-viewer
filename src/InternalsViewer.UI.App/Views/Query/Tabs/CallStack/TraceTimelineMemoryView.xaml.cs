using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class TraceTimelineMemoryView : UserControl
{
    public TraceTimelineMemoryView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnCloseClick(object sender, RoutedEventArgs e) => ViewModel?.Close();

    private void OnChangeNavigationRequested(object? sender, HexChangeNavigation e) => ViewModel?.GoToChange(e.Offset, e.IsForward);
}
