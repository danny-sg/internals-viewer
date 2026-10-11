using System.ComponentModel;
using InternalsViewer.UI.App.Models.Query.Simulation.Steps;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Simulation.Steps;

public sealed partial class SimulationCounterView : UserControl
{
    public SimulationCounterView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bind();
    }

    private SimulationCounter? Counter => DataContext as SimulationCounter;

    private void Bind()
    {
        if (Counter is not { } counter)
        {
            return;
        }

        counter.PropertyChanged -= OnCounterChanged;

        counter.PropertyChanged += OnCounterChanged;

        PairPanel.Visibility = counter.Kind == SimulationCounterKind.Pair ? Visibility.Visible : Visibility.Collapsed;

        TextOnly.Visibility = counter.Kind == SimulationCounterKind.Text ? Visibility.Visible : Visibility.Collapsed;

        Badge.Visibility = counter.Kind is SimulationCounterKind.Badge or SimulationCounterKind.Pill
                           ? Visibility.Visible
                           : Visibility.Collapsed;

        PairName.Text = counter.Name;

        TextOnly.Text = counter.Name;

        Badge.BadgeColour = counter.Colour;

        Badge.BadgeName = counter.Kind == SimulationCounterKind.Pill ? counter.Text : counter.Name;

        Apply();
    }

    private void Apply()
    {
        if (Counter is not { } counter)
        {
            return;
        }

        PairValue.Text = counter.Text;

        if (counter.Kind == SimulationCounterKind.Pill)
        {
            Badge.BadgeName = counter.Text;

            return;
        }

        Badge.BadgeValue = counter.Text;
    }

    private void OnCounterChanged(object? sender, PropertyChangedEventArgs e) => Apply();
}
