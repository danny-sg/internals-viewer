using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Views;

public sealed partial class QueryReplayView : Page
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    private QueryViewModel? _subscribedViewModel;

    public QueryReplayView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;

        EventTimeline.SequenceChanged += OnSequenceChanged;
        EventTimeline.PlayheadChanged += OnPlayheadChanged;
        EventTimeline.PlanNodeSelected += OnTimelinePlanNodeSelected;
        EventTimeline.EventSelected += OnTimelineEventSelected;
        EventTimeline.PlayStateChanged += OnPlayStateChanged;

        PlanRepeater.ElementPrepared += OnPlanElementPrepared;
    }

    private void OnPlayStateChanged(bool isPlaying)
    {
        if (DataContext is QueryViewModel viewModel)
        {
            viewModel.IsTimelinePlaying = isPlaying;
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = DataContext as QueryViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryViewModel.ActivePlanNode))
        {
            ApplyActiveNodeToPlans();
        }
        else if (e.PropertyName == nameof(QueryViewModel.IsTimelinePlaying))
        {
            ApplyPlayingToPlans();
        }
        else if (e.PropertyName == nameof(QueryViewModel.IsError) && ViewModel.IsError)
        {
            //ResultsTabView.SelectedItem = MessagesTab;
        }
    }

    private void OnPlanElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is ExecutionPlanControl planControl)
        {
            planControl.ActiveNode = ViewModel.ActivePlanNode;
            planControl.IsPlaying = ViewModel.IsTimelinePlaying;
        }
    }

    private void ApplyActiveNodeToPlans()
    {
        var node = ViewModel.ActivePlanNode;

        for (var i = 0; i < ViewModel.ExecutionPlans.Count; i++)
        {
            if (PlanRepeater.TryGetElement(i) is ExecutionPlanControl planControl)
            {
                planControl.ActiveNode = node;
            }
        }
    }

    private void ApplyPlayingToPlans()
    {
        var isPlaying = ViewModel.IsTimelinePlaying;

        for (var i = 0; i < ViewModel.ExecutionPlans.Count; i++)
        {
            if (PlanRepeater.TryGetElement(i) is ExecutionPlanControl planControl)
            {
                planControl.IsPlaying = isPlaying;
            }
        }
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var itemCount = ViewModel.DatabaseFiles.Length;

        if (itemCount > 0)
        {
            ViewModel.AllocationMapHeight = AllocationItemRepeater.ActualHeight / itemCount;
        }
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default
                              .Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }

    private void OnSqlTextChanged(object? sender, string sql)
    {
        ViewModel.Sql = sql;
    }

    private void OnSequenceChanged(long sequenceFrom, long sequenceTo)
    {
        ViewModel.SequenceFrom = sequenceFrom;
        ViewModel.SequenceTo = sequenceTo;
    }

    private void OnPlayheadChanged(long playheadSequence)
    {
        ViewModel.PlayheadSequence = playheadSequence;
    }

    private void OnTimelinePlanNodeSelected(PlanNodeIdentifier identifier)
    {
        ViewModel.SelectPlanNode(identifier);
    }

    private void OnTimelineEventSelected(EngineEvent engineEvent)
    {
        ResultsTabView.SelectedItem = EventsTab;
        EventGrid.NavigateToEvent(engineEvent);
    }
}
