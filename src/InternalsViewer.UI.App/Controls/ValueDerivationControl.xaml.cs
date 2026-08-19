using System;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    /// <summary>
    /// Raised when an operand that knows where it came from is clicked
    /// </summary>
    public event EventHandler<DerivationStep>? StepInvoked;

    private void Badge_OnClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is DerivationStep { IsNavigable: true } step)
        {
            StepInvoked?.Invoke(this, step);
        }
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

    /// <summary>
    /// Whether the working is shown, or only the value it came to
    /// </summary>
    public bool ShowSteps
    {
        get => (bool)GetValue(ShowStepsProperty);
        set => SetValue(ShowStepsProperty, value);
    }

    public static readonly DependencyProperty ShowStepsProperty
        = DependencyProperty.Register(nameof(ShowSteps),
                                      typeof(bool),
                                      typeof(ValueDerivationControl),
                                      new PropertyMetadata(true, OnShowStepsChanged));

    private static void OnShowStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ValueDerivationControl)d).ApplySteps();

    /// <summary>
    /// A value stored as it is read back has nothing applied to it, so the working collapses to the value itself
    /// </summary>
    private void ApplySteps()
    {
        var hasSteps = ShowSteps && Derivation is { Steps.Count: > 0 };

        StepsPanel.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
        EqualsText.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
    }

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

        control.ApplySteps();
    }
}
