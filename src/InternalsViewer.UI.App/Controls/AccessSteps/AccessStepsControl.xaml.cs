using System.Collections;
using InternalsViewer.Execution.AccessPaths.Results;
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

    public static readonly DependencyProperty OuterAccentBrushProperty =
        DependencyProperty.Register(nameof(OuterAccentBrush),
                                    typeof(Brush),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null));

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
    /// Colour of the accent bar drawn against steps from a composed access path's outer side
    /// </summary>
    /// <remarks>
    /// When set, side steps are indented under the join's own steps to show rows flowing into the join, with the join's compare and emit
    /// steps at the root level.
    /// </remarks>
    public Brush? OuterAccentBrush
    {
        get => (Brush?)GetValue(OuterAccentBrushProperty);
        set => SetValue(OuterAccentBrushProperty, value);
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
        if (args.Element is not Grid grid || sender.ItemsSourceView?.GetAt(args.Index) is not AccessStep step)
        {
            return;
        }

        var source = step is AccessStep.Rebind ? -1 : step.Source;

        var brush = source switch
        {
            0 => OuterAccentBrush,
            1 => InnerAccentBrush,
            _ => null
        };

        UpdateEmitBadge(grid, brush as SolidColorBrush);

        if (brush is null)
        {
            grid.Margin = new Thickness(0);
            grid.BorderThickness = new Thickness(0);

            return;
        }

        grid.Margin = new Thickness(12, 0, 0, 0);
        grid.BorderBrush = brush;
        grid.BorderThickness = new Thickness(2, 0, 0, 0);
    }

    private static void UpdateEmitBadge(Grid grid, SolidColorBrush? sideBrush)
    {
        if (grid.FindName("EmitBadge") is not Border badge)
        {
            return;
        }

        if (sideBrush is not null)
        {
            badge.Tag ??= new EmitBadgeDefaults(badge.Background, badge.BorderBrush, (badge.Child as TextBlock)?.Foreground);

            badge.Background = sideBrush;
            badge.BorderBrush = new SolidColorBrush(Darken(sideBrush.Color));

            if (badge.Child is TextBlock text)
            {
                text.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x20));
            }
        }
        else if (badge.Tag is EmitBadgeDefaults defaults)
        {
            badge.Background = defaults.Background;
            badge.BorderBrush = defaults.BorderBrush;

            if (badge.Child is TextBlock text && defaults.Foreground is not null)
            {
                text.Foreground = defaults.Foreground;
            }

            badge.Tag = null;
        }
    }

    private static Windows.UI.Color Darken(Windows.UI.Color colour)
    {
        return Windows.UI.Color.FromArgb(colour.A, (byte)(colour.R * 3 / 5), (byte)(colour.G * 3 / 5), (byte)(colour.B * 3 / 5));
    }

    private sealed record EmitBadgeDefaults(Brush Background, Brush BorderBrush, Brush? Foreground);

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
