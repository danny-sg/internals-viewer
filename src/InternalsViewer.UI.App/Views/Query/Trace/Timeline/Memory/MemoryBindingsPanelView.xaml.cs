using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBindingsPanelView : UserControl
{
    public MemoryBindingsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnBindingsClick(object sender, RoutedEventArgs e) => ViewModel?.Bindings.OpenBindings();

    private void OnColumnsClick(object sender, RoutedEventArgs e) => ViewModel?.Bindings.OpenColumns();

    private void OnRowBufferClick(object sender, RoutedEventArgs e) => ViewModel?.Bindings.OpenRowBuffer();
}
