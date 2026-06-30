using System;
using System.ComponentModel;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the execution plan diagrams for the active query.</summary>
public sealed partial class PlanDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    public PlanDocumentView()
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
            case nameof(QueryViewModel.SelectedPlanNode):
                ApplyToPlans(p => p.SelectedNode = _subscribed.SelectedPlanNode);
                break;
            case nameof(QueryViewModel.ActivePlanNodes):
                ApplyToPlans(p => p.ActiveNodes = _subscribed.ActivePlanNodes);
                break;
            case nameof(QueryViewModel.EmittingPlanNodes):
                ApplyToPlans(p => p.EmittingNodes = _subscribed.EmittingPlanNodes);
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

            // ItemsRepeater recycles elements, so guard against subscribing the same control twice.
            planControl.IndexOpenRequested -= OnPlanIndexOpenRequested;
            planControl.IndexOpenRequested += OnPlanIndexOpenRequested;
        }
    }

    private void OnPlanIndexOpenRequested(object? sender, PlanNode node) => ViewModel?.OpenIndex(node);

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
