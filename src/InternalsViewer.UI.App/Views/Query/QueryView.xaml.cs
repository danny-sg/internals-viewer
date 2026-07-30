using System;
using System.ComponentModel;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query;

public sealed partial class QueryView : Page, IDisposable
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    private GridLength _savedTimelineHeight = new(1, GridUnitType.Star);

    private QueryViewModel? _subscribedViewModel;

    public QueryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        EventTimeline.ScopeChanged += OnScopeChanged;
        EventTimeline.PlayheadTimeChanged += OnPlayheadTimeChanged;
        EventTimeline.PlanNodeSelected += OnTimelinePlanNodeSelected;
        EventTimeline.EventSelected += OnTimelineEventSelected;
        EventTimeline.EventDoubleClicked += OnTimelineEventDoubleClicked;
        EventTimeline.IndexOpenRequested += OnTimelineIndexOpenRequested;
        EventTimeline.ExecutionPlanRequested += OnTimelineExecutionPlanRequested;
        EventTimeline.TraceOpenRequested += OnTimelineTraceOpenRequested;
        EventTimeline.PlayStateChanged += OnPlayStateChanged;

        Unloaded += OnUnloaded;
    }

    public void Dispose()
    {
        EventTimeline.ScopeChanged -= OnScopeChanged;
        EventTimeline.PlayheadTimeChanged -= OnPlayheadTimeChanged;
        EventTimeline.PlanNodeSelected -= OnTimelinePlanNodeSelected;
        EventTimeline.EventSelected -= OnTimelineEventSelected;
        EventTimeline.EventDoubleClicked -= OnTimelineEventDoubleClicked;
        EventTimeline.IndexOpenRequested -= OnTimelineIndexOpenRequested;
        EventTimeline.ExecutionPlanRequested -= OnTimelineExecutionPlanRequested;
        EventTimeline.TraceOpenRequested -= OnTimelineTraceOpenRequested;
        EventTimeline.PlayStateChanged -= OnPlayStateChanged;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Layout.PropertyChanged -= OnLayoutPropertyChanged;
            _subscribedViewModel.PlayheadMoveRequested -= OnPlayheadMoveRequested;
            _subscribedViewModel = null;
        }

        (DataContext as QueryViewModel)?.Dispose();

        EventTimeline.Dispose();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Layout.PropertyChanged -= OnLayoutPropertyChanged;
            _subscribedViewModel.PlayheadMoveRequested -= OnPlayheadMoveRequested;
            _subscribedViewModel = null;
        }

        if (DataContext is not QueryViewModel viewModel)
        {
            return;
        }

        DockHostControl.CaptureSizes();

        _ = viewModel.SaveLayoutAsync();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Layout.PropertyChanged -= OnLayoutPropertyChanged;
            _subscribedViewModel.PlayheadMoveRequested -= OnPlayheadMoveRequested;
        }

        _subscribedViewModel = args.NewValue as QueryViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Layout.PropertyChanged += OnLayoutPropertyChanged;
            _subscribedViewModel.PlayheadMoveRequested += OnPlayheadMoveRequested;
            ApplyRowVisibility();
        }
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryLayoutViewModel.IsTimelineVisible)
                           or nameof(QueryLayoutViewModel.IsDetailsVisible))
        {
            ApplyRowVisibility();
        }
    }

    private void ApplyRowVisibility()
    {
        if (ViewModel.Layout is { IsTimelineVisible: true, IsDetailsVisible: true })
        {
            DockRow.Height = new GridLength(1, GridUnitType.Star);
            TimelineRow.Height = _savedTimelineHeight.Value > 0
                                 ? _savedTimelineHeight
                                 : new GridLength(1, GridUnitType.Star);
        }
        else if (ViewModel.Layout is { IsTimelineVisible: true, IsDetailsVisible: false })
        {
            if (TimelineRow.Height.Value > 0)
            {
                _savedTimelineHeight = TimelineRow.Height;
            }

            DockRow.Height = new GridLength(0);
            TimelineRow.Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            if (TimelineRow.Height.Value > 0)
            {
                _savedTimelineHeight = TimelineRow.Height;
            }

            DockRow.Height = new GridLength(1, GridUnitType.Star);
            TimelineRow.Height = new GridLength(0);
        }
    }

    private void OnPlayStateChanged(bool isPlaying)
    {
        if (DataContext is QueryViewModel viewModel)
        {
            viewModel.IsTimelinePlaying = isPlaying;
        }
    }

    private void OnScopeChanged(long fromUs, long toUs)
    {
        ViewModel.SetScope(fromUs, toUs);
    }

    private void OnPlayheadTimeChanged(long timeUs)
    {
        ViewModel.SetPlayheadTime(timeUs);
    }

    private void OnPlayheadMoveRequested(long timeUs)
    {
        EventTimeline.MovePlayheadTo(timeUs);
    }

    private void OnTimelineExecutionPlanRequested(ExecutionOperatorEvent op)
    {
        if (op.PlanNodeIdentifier is { } identifier)
        {
            ViewModel.OpenExecutionPlan(identifier);
        }
    }

    private void OnTimelinePlanNodeSelected(PlanNodeIdentifier identifier)
    {
        ViewModel.SelectPlanNode(identifier);
    }

    private void OnTimelineEventSelected(EngineEvent engineEvent)
    {
        ViewModel.NavigateToEvent(engineEvent);
    }

    private void OnTimelineEventDoubleClicked(EngineEvent engineEvent)
    {
        ViewModel.OpenEventPage(engineEvent);
    }

    private void OnTimelineIndexOpenRequested(ExecutionOperatorEvent op)
    {
        ViewModel.OpenIndex(op);
    }

    private void OnTimelineTraceOpenRequested(ExecutionOperatorEvent op)
    {
        if (op.PlanNodeIdentifier is { } identifier)
        {
            ViewModel.OpenTrace(identifier);
        }
    }
}
