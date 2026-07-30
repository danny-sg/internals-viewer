using System;
using System.ComponentModel;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the execution plan diagrams for the active query.</summary>
public sealed partial class QueryPlanTabView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    private bool _hasFlameGraph = true;

    public bool HasFlameGraph
    {
        get => _hasFlameGraph;
        set
        {
            _hasFlameGraph = value;

            ApplyToPlans(p => p.HasFlameGraph = value);
        }
    }

    public bool IsPropertiesPaneVisible
    {
        get => ViewModel?.IsPlanPropertiesVisible == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.IsPlanPropertiesVisible = value;
            }

            Bindings.Update();
        }
    }

    public GridLength BodyColumnWidth
        => IsPropertiesPaneVisible ? new GridLength(6, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsPropertiesPaneVisible ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailSplitterVisibility
        => IsPropertiesPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    public QueryPlanTabView()
    {
        InitializeComponent();

        // Keep-alive: this view is reused across re-layout, so subscriptions follow the load lifecycle
        // rather than DataContext changes (reparenting fires Unloaded/Loaded without a DataContext change).
        Loaded += OnLoaded;
        Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += OnDataContextChanged;
        PlanRepeater.ElementPrepared += OnPlanElementPrepared;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
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
            p.Events = ViewModel?.Events;
            p.HasFlameGraph = HasFlameGraph;
        });
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
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribed = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_subscribed is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(QueryViewModel.IsPlanPropertiesVisible):
                Bindings.Update();
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
            planControl.Events = viewModel.Events;
            planControl.HasFlameGraph = HasFlameGraph;

            // ItemsRepeater recycles elements, so guard against subscribing the same control twice.
            planControl.IndexOpenRequested -= OnPlanIndexOpenRequested;
            planControl.IndexOpenRequested += OnPlanIndexOpenRequested;

            planControl.PropertiesOpenRequested -= OnPropertiesOpenRequested;
            planControl.PropertiesOpenRequested += OnPropertiesOpenRequested;

            planControl.NodeSelected -= OnPlanNodeSelected;
            planControl.NodeSelected += OnPlanNodeSelected;
        }
    }

    private void CloseDetailPane()
    {
        IsPropertiesPaneVisible = false;
    }

    private void OnPlanIndexOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenIndex(node);

    private void OnPropertiesOpenRequested(object? sender, PlanNode node)
    {
        OnPlanNodeSelected(sender, node);
        IsPropertiesPaneVisible = true;
    }

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
