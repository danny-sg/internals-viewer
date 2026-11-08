using System.ComponentModel;
using System;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.Views.Query.Plan;

/// <summary>Dock document hosting the execution plan diagrams for the active query</summary>
public sealed partial class QueryPlanTabView : UserControl
{
    private const double StepsStar = 2;

    private const double GuideFontSize = 12;

    private const double GuidePaneMinHeight = 200;

    private const double MaximumFitZoom = 1.5;

    private QueryViewModel? _subscribed;

    public QueryPlanTabView()
    {
        InitializeComponent();

        UpdateGuideConfig();

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

    public GridLength BodyColumnWidth
        => IsPropertiesPaneVisible && !IsPropertiesPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsPropertiesPaneVisible && !IsPropertiesPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public GridLength BodyRowHeight
        => IsPropertiesPaneVisible && IsPropertiesPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailRowHeight
        => IsPropertiesPaneVisible && IsPropertiesPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

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

    public GridLength GetStepsWidth(bool isGuideMode, bool isStepsVisible)
        => isGuideMode && isStepsVisible ? new GridLength(StepsStar, GridUnitType.Star) : new GridLength(0);

    public Visibility GetStepsVisibility(bool isGuideMode, bool isStepsVisible)
        => isGuideMode && isStepsVisible ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource? GetIconSource(Uri? icon) => icon is null ? null : new SvgImageSource(icon);

    public GridLength GetGuidePaneHeight(bool isGuideMode) => isGuideMode ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public double GetGuidePaneMinHeight(bool isGuideMode) => isGuideMode ? GuidePaneMinHeight : 0;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        RefreshDetailPane();
        Subscribe();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();

        // Reflect any state that changed while this tab was hidden.
        ApplyToPlans(p =>
        {
            p.SelectedNode = ViewModel?.SelectedPlanNode;
            p.ActiveNodes = ViewModel?.ActivePlanNodes;
            p.EmittingNodes = ViewModel?.EmittingPlanNodes;
            p.IsFullTrace = ViewModel?.IsFullTrace == true;
            p.ShowActivity(ViewModel?.PlanActivity ?? PlanActivity.Empty);
            p.Events = ViewModel?.Events;
            p.AreAnnotationsVisible = ViewModel?.IsAnnotationsVisible == true;
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
            case nameof(QueryViewModel.GuideCodeColour):
                UpdateGuideConfig();
                break;
            case nameof(QueryViewModel.IsAnnotationsVisible):
                ApplyToPlans(p => p.AreAnnotationsVisible = _subscribed.IsAnnotationsVisible);
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
            planControl.IsFullTrace = viewModel.IsFullTrace;
            planControl.ShowActivity(viewModel.PlanActivity);
            planControl.Events = viewModel.Events;
            planControl.AreAnnotationsVisible = viewModel.IsAnnotationsVisible;

            // ItemsRepeater recycles elements, so guard against subscribing the same control twice.
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

    private static MarkdownConfig CreateGuideConfig(string? colour)
    {
        var themes = new MarkdownThemes
        {
            ParagraphMargin = new Thickness(0),
            InlineCodeFontSize = GuideFontSize,
            InlineCodeBorderThickness = new Thickness(0),
            InlineCodePadding = new Thickness(3, 0, 3, 1)
        };

        if (ColourHelpers.FromHex(colour) is { } background)
        {
            themes.InlineCodeBackground = new SolidColorBrush(background);

            var foreground = System.Drawing.Color.White.ContrastingWith(background.ToColor());

            themes.InlineCodeForeground = new SolidColorBrush(foreground.ToWindowsColor());
        }

        return new MarkdownConfig { Themes = themes };
    }

    private void UpdateGuideConfig()
    {
        GuideNameText.Config = CreateGuideConfig(ViewModel?.GuideCodeColour);

        GuideDescriptionText.Config = CreateGuideConfig(ViewModel?.GuideCodeColour);
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

        var zoom = Math.Clamp(Math.Min(PlanScroller.ViewportWidth / width, PlanScroller.ViewportHeight / height),
                              PlanScroller.MinZoomFactor,
                              Math.Min(PlanScroller.MaxZoomFactor, MaximumFitZoom));

        PlanScroller.ChangeView(0, 0, (float)zoom, disableAnimation: true);
    }

    private void CloseDetailPane()
    {
        IsPropertiesPaneVisible = false;
    }

    private void CloseStepsPane()
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsGuideStepsVisible = false;
        }
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

    private void OnPlanIndexOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenIndex(node);

    private void OnPlanSimulationOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenSimulation(node);

    private void OnPlanCallTreeOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenCallTree(node);

    private void OnPropertiesOpenRequested(object? sender, PlanNode node)
    {
        OnPlanNodeSelected(sender, node);
        IsPropertiesPaneVisible = true;
    }

    private void OnCounterToggleRequested(object? sender, PlanNode node) => ViewModel?.ToggleCounter(node);

    private void OnPageOpenRequested(object? sender, PlanPage page) => ViewModel?.OpenPage(page.Page, page.Slot);

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
        if (node is null || sender is not ExecutionPlanControl { Plan: { } plan })
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
