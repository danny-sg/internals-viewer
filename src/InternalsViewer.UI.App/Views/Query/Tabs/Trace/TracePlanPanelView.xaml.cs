using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>
/// The operators the trace runs, with the one whose tab is open marked
/// </summary>
public sealed partial class TracePlanPanelView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TracePlanPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    private void OnNodeSelected(object? sender, PlanNode? node) => ViewModel?.ActivateOperator(node);
}
