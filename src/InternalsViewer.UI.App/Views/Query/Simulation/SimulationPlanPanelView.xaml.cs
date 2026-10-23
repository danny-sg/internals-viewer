using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

/// <summary>
/// The operators the trace runs, with the one whose tab is open marked
/// </summary>
public sealed partial class SimulationPlanPanelView : UserControl
{
    public SimulationPlanPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationTabViewModel? ViewModel => DataContext as SimulationTabViewModel;

    private void OnNodeSelected(object? sender, PlanNode? node) => ViewModel?.ActivateOperator(node);
}
