using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryIteratorPanelView : UserControl
{
    public MemoryIteratorPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnPlanObjectClick(object sender, RoutedEventArgs e) => ViewModel?.Iterator.OpenPlanObject();
}
