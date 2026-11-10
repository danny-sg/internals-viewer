using System.ComponentModel;
using System;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Plan;

/// <summary>Dock document hosting the execution plan diagrams for the active query</summary>
public sealed partial class QueryPlanTabView : UserControl
{
    private const double GuidePaneMinHeight = 200;

    private const double StepIndent = 16;

    private const string ExpandedGlyph = "\uE70D";

    private const string CollapsedGlyph = "\uE76C";

    private const double DefaultBodyShare = 6;

    private const double DefaultDetailShare = 4;

    private const double DefaultPlanShare = 1;

    private const double DefaultStepsShare = 1;

    private const double FullShare = 1;

    private const double MaximumFitZoom = 1.5;

    private const double CleanScaleStep = 0.5;

    private const double ScaleTolerance = 0.000001;

    private const double StickyTolerance = 0.5;

    private QueryViewModel? _subscribed;

    public QueryPlanTabView()
    {
        InitializeComponent();

        // Keep-alive: this view is reused across re-layout, so subscriptions follow the load lifecycle
        // rather than DataContext changes (reparenting fires Unloaded/Loaded without a DataContext change).
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        PlanRepeater.ElementPrepared += OnPlanElementPrepared;

        PlanRepeater.SizeChanged += OnPlanAreaSizeChanged;

        PlanScroller.SizeChanged += OnPlanAreaSizeChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public bool IsPropertiesPaneVisible
    {
        get => ViewModel?.IsPlanPropertiesVisible == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.IsPlanPropertiesVisible = value;
            }

            RefreshDetailPane();
        }
    }

    public bool IsPropertiesPaneDockedBottom
    {
        get => ViewModel?.IsPlanPropertiesDockedBottom == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.IsPlanPropertiesDockedBottom = value;
            }

            RefreshDetailPane();
        }
    }

    public GridLength PlanPaneHeight => GetStar(ViewModel?.Layout.PlanStepsSplit?.First ?? DefaultPlanShare);

    public GridLength BodyColumnWidth
        => GetStar(IsPropertiesPaneVisible && !IsPropertiesPaneDockedBottom
                       ? ViewModel?.Layout.PlanColumnSplit?.First ?? DefaultBodyShare
                       : FullShare);

    public GridLength DetailColumnWidth
        => IsPropertiesPaneVisible && !IsPropertiesPaneDockedBottom
            ? GetStar(ViewModel?.Layout.PlanColumnSplit?.Second ?? DefaultDetailShare)
            : new GridLength(0);

    public GridLength BodyRowHeight
        => GetStar(IsPropertiesPaneVisible && IsPropertiesPaneDockedBottom
                       ? ViewModel?.Layout.PlanRowSplit?.First ?? DefaultBodyShare
                       : FullShare);

    public GridLength DetailRowHeight
        => IsPropertiesPaneVisible && IsPropertiesPaneDockedBottom
            ? GetStar(ViewModel?.Layout.PlanRowSplit?.Second ?? DefaultDetailShare)
            : new GridLength(0);

    public Visibility DetailPaneVisibility
        => IsPropertiesPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColumnSplitterVisibility
        => IsPropertiesPaneVisible && !IsPropertiesPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RowSplitterVisibility
        => IsPropertiesPaneVisible && IsPropertiesPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Thickness DetailPaneBorderThickness
        => IsPropertiesPaneDockedBottom ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);

    public string DockToggleGlyph => IsPropertiesPaneDockedBottom ? "\uE90D" : "\uE90E";

    public string DockToggleTooltip => IsPropertiesPaneDockedBottom ? "Dock Right" : "Dock Bottom";

    private static SolidColorBrush CurrentStepBrush { get; } = new(Colors.White);

    private GuideStepItem? HoveredStep { get; set; }

    private ScrollViewer? GuideScroller { get; set; }

    public static ImageSource? GetStepIcon(Uri? icon) => icon is null ? null : new SvgImageSource(icon);

    public static Brush? GetStepBackground(bool isCurrent) => isCurrent ? CurrentStepBrush : null;

    public static Brush? GetStepRowBackground(bool isCurrent, bool isHovered, bool isSticking)
        => (isCurrent || isSticking) != isHovered ? CurrentStepBrush : null;

    public static Visibility GetCurrentVisibility(bool isCurrent) => isCurrent ? Visibility.Visible : Visibility.Collapsed;

    public static GridLength GetStepIndentWidth(int depth) => new(depth * StepIndent);

    public static Thickness GetStepIndent(int depth) => new(depth * StepIndent, 0, 0, 0);

    public static Visibility GetChevronVisibility(bool hasNestedSteps) => hasNestedSteps ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility GetPredicateVisibility(bool hasPredicate) => hasPredicate ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility GetDescriptionVisibility(bool hasDescription) => hasDescription ? Visibility.Visible : Visibility.Collapsed;

    public static string GetChevronGlyph(bool isExpanded) => isExpanded ? ExpandedGlyph : CollapsedGlyph;

    public static Visibility GetDetailsVisibility(string details) => details.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static string GetPlayLabel(bool isPlaying) => isPlaying ? "Pause" : "Play";

    public static string GetPlayGlyph(bool isPlaying) => isPlaying ? "\uE769" : "\uE768";

    public GridLength GetGuidePaneHeight(bool isGuideMode, bool isVisible)
        => isGuideMode && isVisible ? GetStar(ViewModel?.Layout.PlanStepsSplit?.Second ?? DefaultStepsShare) : new GridLength(0);

    public double GetGuidePaneMinHeight(bool isGuideMode, bool isVisible) => isGuideMode && isVisible ? GuidePaneMinHeight : 0;

    public Visibility GetGuidePaneVisibility(bool isGuideMode, bool isVisible)
        => isGuideMode && isVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetGuideBarVisibility(bool isGuideMode, bool isStepsVisible)
        => isGuideMode && !isStepsVisible ? Visibility.Visible : Visibility.Collapsed;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        RefreshDetailPane();
        Subscribe();

        PlaceGuideCommandBar();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();

        PlaceGuideCommandBar();

        // Reflect any state that changed while this tab was hidden.
        ApplyToPlans(p =>
        {
            p.SelectedNode = ViewModel?.SelectedPlanNode;
            p.ActiveNodes = ViewModel?.ActivePlanNodes;
            p.EmittingNodes = ViewModel?.EmittingPlanNodes;
            p.CurrentStep = ViewModel?.CurrentGuideMarker;
            p.IsFullTrace = ViewModel?.IsFullTrace == true;
            p.ShowActivity(ViewModel?.PlanActivity ?? PlanActivity.Empty);
            p.Events = ViewModel?.Events;
            p.AreAnnotationsVisible = ViewModel?.IsAnnotationsVisible == true;
            p.AreWatchesVisible = ViewModel?.IsPlanWatchesVisible == true;
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            Unsubscribe();
        }
    }

    private void Subscribe()
    {
        if (ReferenceEquals(_subscribed, ViewModel))
        {
            return;
        }

        Unsubscribe();

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;

            _subscribed.PlanActivityChanged += OnPlanActivityChanged;

            _subscribed.Layout.PropertyChanged += OnLayoutPropertyChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;

            _subscribed.PlanActivityChanged -= OnPlanActivityChanged;

            _subscribed.Layout.PropertyChanged -= OnLayoutPropertyChanged;

            _subscribed = null;
        }
    }

    private void OnPlanActivityChanged(PlanActivity activity) => ApplyToPlans(p => p.ShowActivity(activity));

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_subscribed is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(QueryViewModel.IsPlanPropertiesVisible):
            case nameof(QueryViewModel.IsPlanPropertiesDockedBottom):
                RefreshDetailPane();
                break;
            case nameof(QueryViewModel.CurrentGuideStep):
                ScrollToCurrentStep();
                break;
            case nameof(QueryViewModel.GuideStepGroups):
                DispatcherQueue.TryEnqueue(UpdateStickyStep);
                break;
            case nameof(QueryViewModel.IsGuideStepsVisible):
                PlaceGuideCommandBar();
                DispatcherQueue.TryEnqueue(ScrollToCurrentStep);
                break;
            case nameof(QueryViewModel.IsAnnotationsVisible):
                ApplyToPlans(p => p.AreAnnotationsVisible = _subscribed.IsAnnotationsVisible);
                break;
            case nameof(QueryViewModel.IsPlanWatchesVisible):
                ApplyToPlans(p => p.AreWatchesVisible = _subscribed.IsPlanWatchesVisible);
                break;
            case nameof(QueryViewModel.SelectedPlanNode):
                ApplyToPlans(p => p.SelectedNode = _subscribed.SelectedPlanNode);
                break;
            case nameof(QueryViewModel.ActivePlanNodes):
                ApplyToPlans(p => p.ActiveNodes = _subscribed.ActivePlanNodes);
                break;
            case nameof(QueryViewModel.EmittingPlanNodes):
                ApplyToPlans(p => p.EmittingNodes = _subscribed.EmittingPlanNodes);
                break;
            case nameof(QueryViewModel.CurrentGuideMarker):
                ApplyToPlans(p => p.CurrentStep = _subscribed.CurrentGuideMarker);
                break;
            case nameof(QueryViewModel.IsFullTrace):
                ApplyToPlans(p => p.IsFullTrace = _subscribed.IsFullTrace);
                break;
            case nameof(QueryViewModel.Events):
                ApplyToPlans(p => p.Events = _subscribed.Events);
                break;
        }
    }

    private void OnPlanElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is ExecutionPlanControl planControl && ViewModel is { } viewModel)
        {
            planControl.SelectedNode = viewModel.SelectedPlanNode;
            planControl.ActiveNodes = viewModel.ActivePlanNodes;
            planControl.EmittingNodes = viewModel.EmittingPlanNodes;
            planControl.CurrentStep = viewModel.CurrentGuideMarker;
            planControl.IsFullTrace = viewModel.IsFullTrace;
            planControl.ShowActivity(viewModel.PlanActivity);
            planControl.Events = viewModel.Events;
            planControl.AreAnnotationsVisible = viewModel.IsAnnotationsVisible;
            planControl.AreWatchesVisible = viewModel.IsPlanWatchesVisible;

            // ItemsRepeater recycles elements, so guard against subscribing the same control twice.
            planControl.StepLabelClicked -= OnPlanStepLabelClicked;
            planControl.StepLabelClicked += OnPlanStepLabelClicked;

            planControl.IndexOpenRequested -= OnPlanIndexOpenRequested;
            planControl.IndexOpenRequested += OnPlanIndexOpenRequested;


            planControl.SimulationOpenRequested -= OnPlanSimulationOpenRequested;
            planControl.SimulationOpenRequested += OnPlanSimulationOpenRequested;

            planControl.CallTreeOpenRequested -= OnPlanCallTreeOpenRequested;
            planControl.CallTreeOpenRequested += OnPlanCallTreeOpenRequested;

            planControl.PropertiesOpenRequested -= OnPropertiesOpenRequested;
            planControl.PropertiesOpenRequested += OnPropertiesOpenRequested;

            planControl.NodeSelected -= OnPlanNodeSelected;
            planControl.NodeSelected += OnPlanNodeSelected;

            planControl.CounterToggleRequested -= OnCounterToggleRequested;
            planControl.CounterToggleRequested += OnCounterToggleRequested;

            planControl.PageOpenRequested -= OnPageOpenRequested;
            planControl.PageOpenRequested += OnPageOpenRequested;

            planControl.IsCounterShown = viewModel.IsCounterShown;
        }
    }

    private void ScrollToCurrentStep()
    {
        if (ViewModel is not { IsGuideStepsVisible: true, CurrentGuideStep: { IsShown: true } step })
        {
            return;
        }

        if (GuideStepList.ContainerFromItem(step) is not ListViewItem container
            || GuideStepList.FindDescendant<ScrollViewer>() is not { Content: UIElement content } scroller)
        {
            GuideStepList.ScrollIntoView(step, ScrollIntoViewAlignment.Leading);

            return;
        }

        var top = container.TransformToVisual(content).TransformPoint(new Point(0, 0)).Y;

        var offset = top - GetHeadingHeight(container) - GetStickyHeight(step);

        scroller.ChangeView(null, Math.Max(0, offset), null, disableAnimation: false);
    }

    private double GetHeadingHeight(ListViewItem container)
        => GuideStepList.GroupHeaderContainerFromItemContainer(container) is FrameworkElement heading ? heading.ActualHeight : 0;

    private double GetStickyHeight(GuideStepItem step)
    {
        if (step.Parent is not { IsSticky: true } sticky)
        {
            return 0;
        }

        return GuideStepList.ContainerFromItem(sticky) is FrameworkElement container ? container.ActualHeight : StickyStepHost.ActualHeight;
    }

    private void OnGuideStepListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (GuideScroller is null && GuideStepList.FindDescendant<ScrollViewer>() is { } scroller)
        {
            GuideScroller = scroller;

            scroller.ViewChanged += OnGuideStepsScrolled;
        }

        UpdateStickyStep();
    }

    private void OnGuideStepsScrolled(object? sender, ScrollViewerViewChangedEventArgs e) => UpdateStickyStep();

    private void UpdateStickyStep()
    {
        var sticky = GetStickyStep();

        if (!ReferenceEquals(StickyStep.Content, sticky))
        {
            (StickyStep.Content as GuideStepItem)?.IsSticking = false;

            sticky?.IsSticking = true;

            StickyStep.Content = sticky;
        }

        StickyStepHost.Visibility = sticky is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private GuideStepItem? GetStickyStep()
    {
        if (GuideStepList.ItemsPanelRoot is not ItemsStackPanel { FirstVisibleIndex: >= 0 } panel)
        {
            return null;
        }

        for (var index = panel.FirstVisibleIndex; index <= panel.LastVisibleIndex; index++)
        {
            if (GuideStepList.ContainerFromIndex(index) is not ListViewItem { Content: GuideStepItem step } container)
            {
                continue;
            }

            var heading = GetHeadingHeight(container);

            var top = container.TransformToVisual(GuideStepList).TransformPoint(new Point(0, 0)).Y;

            if (top + container.ActualHeight <= heading)
            {
                continue;
            }

            StickyStepHost.Margin = new Thickness(0, heading, 0, 0);

            if (step.IsSticky)
            {
                return top < heading - StickyTolerance ? step : null;
            }

            return step.Parent is { IsSticky: true } parent ? parent : null;
        }

        return null;
    }

    private void OnStickyStepTapped(object sender, TappedRoutedEventArgs e)
    {
        if (StickyStep.Content is GuideStepItem step)
        {
            ViewModel?.SelectGuideStep(step);
        }
    }

    private void OnGuideStepPointerEntered(object sender, PointerRoutedEventArgs e)
        => SetHoveredStep((sender as FrameworkElement)?.DataContext as GuideStepItem);

    private void OnGuideStepPointerExited(object sender, PointerRoutedEventArgs e) => SetHoveredStep(null);

    private void SetHoveredStep(GuideStepItem? step)
    {
        if (ReferenceEquals(step, HoveredStep))
        {
            return;
        }

        HoveredStep?.IsHovered = false;

        HoveredStep = step;

        HoveredStep?.IsHovered = true;
    }

    private void OnGuideStepToggle(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GuideStepItem step })
        {
            ViewModel?.ToggleGuideStep(step);

            DispatcherQueue.TryEnqueue(UpdateStickyStep);
        }
    }

    private void OnGuideStepClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GuideStepItem step)
        {
            ViewModel?.SelectGuideStep(step);
        }
    }

    private void OnGuideLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (TraceTickTracks.TryGetPageLink(e.Uri, out var page, out var slot))
        {
            ViewModel?.OpenPage(page, slot);
        }
        else if (TraceTickTracks.TryGetHashLink(e.Uri, out var group, out var partition))
        {
            ViewModel?.OpenHashTable(group, partition);
        }
        else
        {
            return;
        }

        e.Handled = true;

        if (sender is MarkdownTextBlock block)
        {
            DispatcherQueue.TryEnqueue(() => Rerender(block));
        }
    }

    private static void Rerender(MarkdownTextBlock block)
    {
        var text = block.Text;

        block.Text = string.Empty;

        block.Text = text;
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryLayoutViewModel.IsGuideMode))
        {
            DispatcherQueue.TryEnqueue(FitPlan);
        }
    }

    private void OnPlanAreaSizeChanged(object sender, SizeChangedEventArgs e) => FitPlan();

    private void FitPlan()
    {
        if (ViewModel?.Layout.IsGuideMode != true
            || PlanRepeater.ActualWidth <= 0
            || PlanRepeater.ActualHeight <= 0
            || PlanScroller.ViewportWidth <= 0
            || PlanScroller.ViewportHeight <= 0)
        {
            return;
        }

        var width = PlanRepeater.ActualWidth + PlanScroller.Padding.Left + PlanScroller.Padding.Right;

        var height = PlanRepeater.ActualHeight + PlanScroller.Padding.Top + PlanScroller.Padding.Bottom;

        var fit = Math.Min(Math.Min(PlanScroller.ViewportWidth / width, PlanScroller.ViewportHeight / height),
                           Math.Min(PlanScroller.MaxZoomFactor, MaximumFitZoom));

        var zoom = Math.Max(GetCleanZoom(fit), PlanScroller.MinZoomFactor);

        PlanScroller.ChangeView(0, 0, (float)zoom, disableAnimation: true);

        ApplyToPlans(p => p.ScheduleRenderRefresh());
    }

    private double GetCleanZoom(double zoom)
    {
        var rasterizationScale = XamlRoot?.RasterizationScale ?? 1;

        var scale = (zoom * rasterizationScale) + ScaleTolerance;

        var cleanScale = scale >= 1
            ? Math.Floor(scale / CleanScaleStep) * CleanScaleStep
            : Math.Pow(2, Math.Floor(Math.Log2(scale)));

        return cleanScale / rasterizationScale;
    }

    private static GridLength GetStar(double share) => new(share, GridUnitType.Star);

    private void OnSplitterMoved(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Layout is not { } layout)
        {
            return;
        }

        if (ReferenceEquals(sender, StepsSplitter))
        {
            layout.PlanStepsSplit = new PaneSplit(PlanRow.Height.Value, StepsRow.Height.Value);
        }
        else if (ReferenceEquals(sender, DetailColumnSplitter))
        {
            layout.PlanColumnSplit = new PaneSplit(BodyColumn.Width.Value, DetailColumn.Width.Value);
        }
        else if (ReferenceEquals(sender, DetailRowSplitter))
        {
            layout.PlanRowSplit = new PaneSplit(BodyRow.Height.Value, DetailRow.Height.Value);
        }
    }

    private void CloseStepsPane() => ViewModel?.IsGuideStepsVisible = false;

    private void PlaceGuideCommandBar()
    {
        var host = ViewModel?.IsGuideStepsVisible == true ? StepsCommandBarHost : PlanCommandBarHost;

        if (ReferenceEquals(host.Child, GuideCommandBar))
        {
            return;
        }

        PlanCommandBarHost.Child = null;

        StepsCommandBarHost.Child = null;

        host.Child = GuideCommandBar;
    }

    private void CloseDetailPane()
    {
        IsPropertiesPaneVisible = false;
    }

    private void ToggleDetailPaneDock()
    {
        IsPropertiesPaneDockedBottom = !IsPropertiesPaneDockedBottom;
    }

    private void RefreshDetailPane()
    {
        Grid.SetRow(DetailPane, IsPropertiesPaneDockedBottom ? 2 : 0);
        Grid.SetColumn(DetailPane, IsPropertiesPaneDockedBottom ? 0 : 2);

        Bindings.Update();
    }

    private void OnPlanStepLabelClicked(object? sender, bool isBackward) => ViewModel?.GuidePlayback.Step(forward: !isBackward);

    private void OnPlanBackgroundTapped(object sender, TappedRoutedEventArgs e) => ClearPlanSelection();

    private void ClearPlanSelection() => ViewModel?.SelectedPlanNode = null;

    private void OnPlanContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var menu = PlanViewMenu.Create(viewModel);

        if (e.TryGetPosition(sender, out var position))
        {
            menu.ShowAt(sender, new FlyoutShowOptions { Position = position });
        }
        else
        {
            menu.ShowAt((FrameworkElement)sender);
        }

        e.Handled = true;
    }

    private void OnPlanIndexOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenIndex(node);

    private void OnPlanSimulationOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenSimulation(node);

    private void OnPlanCallTreeOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenCallTree(node);

    private void OnPropertiesOpenRequested(object? sender, PlanNode node)
    {
        OnPlanNodeSelected(sender, node);
        IsPropertiesPaneVisible = true;
    }

    private void OnCounterToggleRequested(object? sender, PlanNode node) => ViewModel?.ToggleCounter(node);

    private void OnPageOpenRequested(object? sender, PlanPage page) => ViewModel?.OpenOperatorPage(page);

    /// <summary>
    /// Routes a click on a plan node through the same selection every other view goes through
    /// </summary>
    /// <remarks>
    /// The control already tracked the click in its own highlight; what it could not do is tell anything else, since
    /// only the view model knows the node is also an event. SelectPlanNode sets both, so the callstack focuses the
    /// operator and the details panel follows — the plan becoming a way IN to the stack rather than a picture beside it.
    ///
    /// The identifier rather than the node: a PlanNode knows its own id but not which plan it belongs to, and the view
    /// model keys everything on both.
    /// </remarks>
    private void OnPlanNodeSelected(object? sender, PlanNode? node)
    {
        if (node is null)
        {
            ClearPlanSelection();

            return;
        }

        if (sender is not ExecutionPlanControl { Plan: { } plan })
        {
            return;
        }

        ViewModel?.SelectPlanNode(new PlanNodeIdentifier(plan.PlanHandleId, node.NodeId));
    }

    private void ApplyToPlans(Action<ExecutionPlanControl> apply)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        for (var i = 0; i < viewModel.ExecutionPlans.Count; i++)
        {
            if (PlanRepeater.TryGetElement(i) is ExecutionPlanControl planControl)
            {
                apply(planControl);
            }
        }
    }
}
