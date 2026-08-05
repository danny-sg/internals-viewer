using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.UI.App.Models.Trace;
using Microsoft.UI.Xaml.Controls;

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

    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register(nameof(Nodes),
                                    typeof(object),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnNodesChanged));

    public static readonly DependencyProperty ShowDetailProperty =
        DependencyProperty.Register(nameof(ShowDetail),
                                    typeof(bool),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(false, OnShowDetailChanged));

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

    public bool ShowDetail
    {
        get => (bool)GetValue(ShowDetailProperty);
        set => SetValue(ShowDetailProperty, value);
    }

    private static void OnShowDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccessStepsControl)d).UpdateDetailLayout();
    }

    private bool _isDetailVisible;

    private GridLength _detailHeight = new(180);

    private void UpdateDetailLayout()
    {
        var isVisible = ShowDetail && CurrentStep is not null;

        if (isVisible == _isDetailVisible)
        {
            return;
        }

        _isDetailVisible = isVisible;

        if (isVisible)
        {
            DetailArea.Visibility = Visibility.Visible;
            DetailSplitter.Visibility = Visibility.Visible;

            DetailRow.Height = _detailHeight;

            return;
        }

        if (DetailRow.Height.IsAbsolute && DetailRow.Height.Value > 0)
        {
            _detailHeight = DetailRow.Height;
        }

        DetailArea.Visibility = Visibility.Collapsed;
        DetailSplitter.Visibility = Visibility.Collapsed;

        DetailRow.Height = new GridLength(0);
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

        _styler.ApplyNodeStyling(grid, step, applyIndent: true, showName: showName);
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

            control.DispatcherQueue.TryEnqueue(control.StyleDetail);
        }

        control.UpdateDetailLayout();
    }

    private void StyleDetail()
    {
        if (CurrentStep is not { } step)
        {
            return;
        }

        var node = _styler.NodeFor(step.NodeId);

        DetailName.Text = node?.Name ?? string.Empty;

        DetailSubtitle.Text = node?.Subtitle ?? string.Empty;
        DetailSubtitle.Visibility = DetailSubtitle.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        DetailBlob.Background = _styler.BlobBrushFor(step.NodeId);

        DetailDescription.Text = TraceStepDescriber.Describe(step, Nodes);

        if (DetailHost.ContentTemplateRoot is Grid grid)
        {
            _styler.ApplyNodeStyling(grid, step, applyIndent: false, showName: false);
        }
    }
}
