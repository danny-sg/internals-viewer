using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationStepsPanelView : UserControl
{
    public SimulationStepsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationTabViewModel? ViewModel => DataContext as SimulationTabViewModel;

    public Visibility EmptyStepsVisibility(int count, bool isRunningToEnd)
        => count == 0 && !isRunningToEnd ? Visibility.Visible : Visibility.Collapsed;

    private void OnNodeActivated(object? sender, int nodeId) => ViewModel?.ActivateOperator(nodeId);
}
