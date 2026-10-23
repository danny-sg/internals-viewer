using InternalsViewer.UI.App.Models.Query.Simulation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

public sealed partial class SimulationHashTablePairView : UserControl
{
    public SimulationHashTablePairView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public SimulationHashTablePair? ViewModel => DataContext as SimulationHashTablePair;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is not { } pair)
        {
            return;
        }

        LocalPanel.DataContext = pair.Local;

        GlobalPanel.DataContext = pair.Global;
    }
}
