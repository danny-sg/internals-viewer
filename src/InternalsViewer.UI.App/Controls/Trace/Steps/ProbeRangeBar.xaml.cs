using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

public sealed partial class ProbeRangeBar : UserControl
{
    public static readonly DependencyProperty ProbeProperty =
        DependencyProperty.Register(nameof(Probe),
                                    typeof(AccessStep.Probe),
                                    typeof(ProbeRangeBar),
                                    new PropertyMetadata(null, OnProbeChanged));

    public AccessStep.Probe? Probe
    {
        get => (AccessStep.Probe?)GetValue(ProbeProperty);
        set => SetValue(ProbeProperty, value);
    }

    public ProbeRangeBar()
    {
        InitializeComponent();
    }

    private static void OnProbeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ProbeRangeBar)d).Rebuild();
    }

    private void Rebuild()
    {
        BarGrid.Children.Clear();
        BarGrid.ColumnDefinitions.Clear();

        if (Probe is not { SlotCount: > 0 } probe)
        {
            return;
        }

        var outOfScope = new SolidColorBrush(Colors.Gray);
        var eliminated = BrushFor("SystemFillColorCriticalBrush", Color.FromArgb(255, 196, 43, 28));
        var remaining = BrushFor("SystemFillColorSuccessBrush", Color.FromArgb(255, 15, 123, 15));
        var marker = BrushFor("TextFillColorPrimaryBrush", Color.FromArgb(255, 96, 96, 96));

        var beforeMid = probe.Middle - probe.Low;
        var afterMid = probe.High - probe.Middle - 1;

        AddSegment(probe.Low, outOfScope);
        AddSegment(beforeMid, probe.SearchRight ? eliminated : remaining);
        AddSegment(1, probe.SearchRight ? eliminated : remaining, minWidth: 2);
        AddSegment(afterMid, probe.SearchRight ? remaining : eliminated);
        AddSegment(probe.SlotCount - probe.High, outOfScope);

        var tick = new Polygon
        {
            Points = [new Point(0, 4), new Point(6, 4), new Point(3, 0)],
            Fill = marker,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };

        Grid.SetRow(tick, 1);
        Grid.SetColumn(tick, 2);

        BarGrid.Children.Add(tick);
    }

    private void AddSegment(int length, Brush brush, double minWidth = 0)
    {
        BarGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(Math.Max(length, 0), GridUnitType.Star),
            MinWidth = minWidth
        });

        var rectangle = new Rectangle { Fill = brush };

        Grid.SetColumn(rectangle, BarGrid.ColumnDefinitions.Count - 1);

        BarGrid.Children.Add(rectangle);
    }

    private static Brush BrushFor(string key, Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
