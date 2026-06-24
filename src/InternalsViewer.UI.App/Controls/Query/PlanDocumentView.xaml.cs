using System;
using System.ComponentModel;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query;

/// <summary>Dock document hosting the execution plan diagrams for the active query.</summary>
public sealed partial class PlanDocumentView : UserControl
{
    public QueryViewModel? ViewModel => (DataContext as DocumentViewModel)?.Query ?? DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    public PlanDocumentView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Unsubscribe();
        PlanRepeater.ElementPrepared += OnPlanElementPrepared;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

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

        if (e.PropertyName == nameof(QueryViewModel.ActivePlanNode))
        {
            ApplyToPlans(p => p.ActiveNode = _subscribed.ActivePlanNode);
        }
        else if (e.PropertyName == nameof(QueryViewModel.IsTimelinePlaying))
        {
            ApplyToPlans(p => p.IsPlaying = _subscribed.IsTimelinePlaying);
        }
    }

    private void OnPlanElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is ExecutionPlanControl planControl && ViewModel is { } viewModel)
        {
            planControl.ActiveNode = viewModel.ActivePlanNode;
            planControl.IsPlaying = viewModel.IsTimelinePlaying;
        }
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
