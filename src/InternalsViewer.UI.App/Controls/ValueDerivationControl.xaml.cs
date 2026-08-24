using System;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// Shows the working behind a value, being what was stored, what was applied to it and what it came to
/// </summary>
public sealed partial class ValueDerivationControl
{
    private readonly StepSlot[] _slots;

    public ValueDerivationControl()
    {
        InitializeComponent();

        _slots =
        [
            new StepSlot(Step0, Step0Operator, Step0Badge, Step0Name, Step0Value),
            new StepSlot(Step1, Step1Operator, Step1Badge, Step1Name, Step1Value),
            new StepSlot(Step2, Step2Operator, Step2Badge, Step2Name, Step2Value)
        ];
    }

    /// <summary>
    /// Raised when an operand that knows where it came from is clicked
    /// </summary>
    public event EventHandler<DerivationStep>? StepInvoked;

    /// <summary>
    /// Raised when a result that knows where it lives is clicked
    /// </summary>
    public event EventHandler<ValueDerivation>? ResultInvoked;

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

    private void Result_OnClick(object sender, RoutedEventArgs e)
    {
        if (Derivation is { IsNavigable: true } derivation)
        {
            ResultInvoked?.Invoke(this, derivation);
        }
    }

    private void Badge_OnClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is DerivationStep { IsNavigable: true } step)
        {
            StepInvoked?.Invoke(this, step);
        }
    }

    /// <summary>
    /// A value stored as it is read back has nothing applied to it, so the working collapses to the value itself
    /// </summary>
    private void ApplySteps()
    {
        var steps = Derivation?.Steps ?? [];

        var hasSteps = ShowSteps && steps.Count > 0;

        StepsPanel.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
        EqualsText.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;

        var resultMargin = hasSteps ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 8, 0);

        ResultText.Margin = resultMargin;
        ResultLink.Margin = resultMargin;

        for (var index = 0; index < _slots.Length; index++)
        {
            _slots[index].Apply(hasSteps && index < steps.Count ? steps[index] : null);
        }
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

        control.ResultLink.Content = derivation.Result;

        control.ResultText.Visibility = derivation.IsNavigable ? Visibility.Collapsed : Visibility.Visible;

        control.ResultLink.Visibility = derivation.IsNavigable ? Visibility.Visible : Visibility.Collapsed;

        control.ApplySteps();
    }

    private sealed class StepSlot(StackPanel root, TextBlock op, Button badge, TextBlock name, TextBlock value)
    {
        public void Apply(DerivationStep? step)
        {
            if (step is null)
            {
                root.Visibility = Visibility.Collapsed;

                badge.Tag = null;

                return;
            }

            op.Text = step.Operator;

            name.Text = step.Name;

            value.Text = step.Value;

            badge.Tag = step;
            badge.IsTabStop = step.IsNavigable;
            badge.IsHitTestVisible = step.IsNavigable;

            root.Visibility = Visibility.Visible;
        }
    }
}
