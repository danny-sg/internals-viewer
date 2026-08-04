using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceStepsPanelView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TraceStepsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public Visibility EmptyStepsVisibility(int count, bool isRunningToEnd)
        => count == 0 && !isRunningToEnd ? Visibility.Visible : Visibility.Collapsed;

    private void OnNodeActivated(object? sender, int nodeId) => ViewModel?.ActivateOperator(nodeId);
}
