using System.Collections;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class AccessStepsControl : UserControl
{
    public static readonly DependencyProperty StepHistoryProperty =
        DependencyProperty.Register(nameof(StepHistory),
                                    typeof(IEnumerable),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep),
                                    typeof(AccessStep),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnCurrentStepChanged));

    public static readonly DependencyProperty InnerAccentBrushProperty =
        DependencyProperty.Register(nameof(InnerAccentBrush),
                                    typeof(Brush),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null));

    public AccessStepsControl()
    {
        InitializeComponent();

        StepsList.ElementPrepared += OnElementPrepared;
    }

    /// <summary>
    /// Colour of the accent bar drawn against steps from a composed access path's inner side
    /// </summary>
    public Brush? InnerAccentBrush
    {
        get => (Brush?)GetValue(InnerAccentBrushProperty);
        set => SetValue(InnerAccentBrushProperty, value);
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (InnerAccentBrush is { } brush && args.Element is Grid grid)
        {
            grid.BorderBrush = brush;
        }
    }

    /// <summary>
    /// The full history of steps taken by the access path
    /// </summary>
    public IEnumerable? StepHistory
    {
        get => (IEnumerable?)GetValue(StepHistoryProperty);
        set => SetValue(StepHistoryProperty, value);
    }

    /// <summary>
    /// The most recently taken step
    /// </summary>
    public AccessStep? CurrentStep
    {
        get => (AccessStep?)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AccessStepsControl)d;

        if (e.NewValue is not null)
        {
            control.StepsScroller.ChangeView(null, 0, null, true);
        }
    }
}
