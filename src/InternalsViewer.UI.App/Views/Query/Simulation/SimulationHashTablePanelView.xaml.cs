using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

/// <summary>
/// The hash table of one hash match, shown under the build side that fills it
/// </summary>
public sealed partial class SimulationHashTablePanelView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title),
                                    typeof(string),
                                    typeof(SimulationHashTablePanelView),
                                    new PropertyMetadata("Hash Table"));

    public static readonly DependencyProperty SubTitleProperty =
        DependencyProperty.Register(nameof(SubTitle),
                                    typeof(string),
                                    typeof(SimulationHashTablePanelView),
                                    new PropertyMetadata(string.Empty));

    public SimulationHashTablePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationHashTableViewModel? ViewModel => DataContext as SimulationHashTableViewModel;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SubTitle
    {
        get => (string)GetValue(SubTitleProperty);
        set => SetValue(SubTitleProperty, value);
    }
}
