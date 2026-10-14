using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.CallStack;

public static class SourceStepText
{
    public static readonly DependencyProperty StepProperty
        = DependencyProperty.RegisterAttached("Step",
                                              typeof(SourceStep),
                                              typeof(SourceStepText),
                                              new PropertyMetadata(null, OnStepChanged));

    public static SourceStep? GetStep(DependencyObject element) => (SourceStep?)element.GetValue(StepProperty);

    public static void SetStep(DependencyObject element, SourceStep? value) => element.SetValue(StepProperty, value);

    private static Brush? SecondaryBrush
        => Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush) ? brush as Brush : null;

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        text.Inlines.Clear();

        if (e.NewValue is not SourceStep step)
        {
            return;
        }

        SignatureRuns.AddFunction(text.Inlines, step.Node.Symbol);

        if (step.Id != 0)
        {
            text.Inlines.Add(new Run { Text = $"  [{step.Id}]", Foreground = SecondaryBrush });
        }
    }
}
