using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

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

    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register(nameof(Nodes),
                                    typeof(object),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnNodesChanged));

    public static readonly DependencyProperty BlobPaletteProperty =
        DependencyProperty.Register(nameof(BlobPalette),
                                    typeof(TraceBlobPalette),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnBlobPaletteChanged));

    public TraceBlobPalette? BlobPalette
    {
        get => (TraceBlobPalette?)GetValue(BlobPaletteProperty);
        set => SetValue(BlobPaletteProperty, value);
    }

    private static void OnBlobPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AccessStepsControl)d;

        control._styler.Palette = control.BlobPalette;
    }

    private readonly StepRowStyler _styler = new();

    public AccessStepsControl()
    {
        InitializeComponent();

        _styler.NodeActivated = nodeId => NodeActivated?.Invoke(this, nodeId);

        StepsList.ElementPrepared += OnElementPrepared;
        StepsList.ElementIndexChanged += OnElementIndexChanged;
    }

    public event EventHandler<int>? NodeActivated;

    /// <summary>
    /// What each plan node shows as in the timeline - its name, its colour and how deep it sits in the operator tree
    /// </summary>
    public IReadOnlyDictionary<int, TraceStepNode>? Nodes
    {
        get => (IReadOnlyDictionary<int, TraceStepNode>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    private static void OnNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AccessStepsControl)d;

        control._styler.SetNodes(control.Nodes);
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        => StyleRow(args.Element as Grid, args.Index);

    private void OnElementIndexChanged(ItemsRepeater sender, ItemsRepeaterElementIndexChangedEventArgs args)
        => StyleRow(args.Element as Grid, args.NewIndex);

    private void StyleRow(Grid? grid, int index)
    {
        if (grid is null
            || StepsList.ItemsSourceView is not { } view
            || index < 0
            || index >= view.Count
            || view.GetAt(index) is not AccessStep step)
        {
            return;
        }

        var showName = index == 0
                       || view.GetAt(index - 1) is not AccessStep newer
                       || newer.NodeId != step.NodeId;

        _styler.ApplyNodeStyling(grid, step, showName);
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
        if (e.NewValue is null)
        {
            return;
        }

        ((AccessStepsControl)d).StepsScroller.ChangeView(null, 0, null, true);
    }
}
