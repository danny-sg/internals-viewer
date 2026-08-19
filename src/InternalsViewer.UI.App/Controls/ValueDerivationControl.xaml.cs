using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// Shows the working behind a value, being what was stored, what was applied to it and what it came to
/// </summary>
public sealed partial class ValueDerivationControl
{
    public ValueDerivationControl()
    {
        InitializeComponent();
    }

    public ValueDerivation? Derivation
    {
        get => (ValueDerivation?)GetValue(DerivationProperty);
        set => SetValue(DerivationProperty, value);
    }

    public static readonly DependencyProperty DerivationProperty
        = DependencyProperty.Register(nameof(Derivation),
                                      typeof(ValueDerivation),
                                      typeof(ValueDerivationControl),
                                      new PropertyMetadata(null, OnDerivationChanged));

    private static void OnDerivationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ValueDerivationControl)d;

        var derivation = e.NewValue as ValueDerivation;

        control.Root.Visibility = derivation is null ? Visibility.Collapsed : Visibility.Visible;

        if (derivation is null)
        {
            return;
        }

        control.ResultText.Text = derivation.Result;

        control.StepItems.ItemsSource = derivation.Steps;

        control.StepsPanel.Visibility = derivation.Steps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
