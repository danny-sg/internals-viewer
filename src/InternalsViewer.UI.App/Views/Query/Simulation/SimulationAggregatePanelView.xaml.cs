using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationAggregatePanelView : UserControl
{
    public SimulationAggregatePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationAggregateViewModel? ViewModel => DataContext as SimulationAggregateViewModel;
}
