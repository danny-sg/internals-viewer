using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationTabCommands : UserControl
{
    public SimulationTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationTabViewModel? ViewModel => DataContext as SimulationTabViewModel;

}
