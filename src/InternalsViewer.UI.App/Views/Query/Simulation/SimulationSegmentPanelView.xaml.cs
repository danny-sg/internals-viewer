using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationSegmentPanelView : UserControl
{
    public SimulationSegmentPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationSegmentViewModel? ViewModel => DataContext as SimulationSegmentViewModel;
}
