using System.ComponentModel;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

public sealed partial class TraceCounterView : UserControl
{
    public TraceCounterView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bind();
    }

    private TraceCounter? Counter => DataContext as TraceCounter;

    private void Bind()
    {
        if (Counter is not { } counter)
        {
            return;
        }

        counter.PropertyChanged -= OnCounterChanged;

        counter.PropertyChanged += OnCounterChanged;

        PairPanel.Visibility = counter.Kind == TraceCounterKind.Pair ? Visibility.Visible : Visibility.Collapsed;

        TextOnly.Visibility = counter.Kind == TraceCounterKind.Text ? Visibility.Visible : Visibility.Collapsed;

        Badge.Visibility = counter.Kind is TraceCounterKind.Badge or TraceCounterKind.Pill
                           ? Visibility.Visible
                           : Visibility.Collapsed;

        PairName.Text = counter.Name;

        TextOnly.Text = counter.Name;

        Badge.BadgeColour = counter.Colour;

        Badge.BadgeName = counter.Kind == TraceCounterKind.Pill ? counter.Text : counter.Name;

        Apply();
    }

    private void Apply()
    {
        if (Counter is not { } counter)
        {
            return;
        }

        PairValue.Text = counter.Text;

        if (counter.Kind == TraceCounterKind.Pill)
        {
            Badge.BadgeName = counter.Text;

            return;
        }

        Badge.BadgeValue = counter.Text;
    }

    private void OnCounterChanged(object? sender, PropertyChangedEventArgs e) => Apply();
}
