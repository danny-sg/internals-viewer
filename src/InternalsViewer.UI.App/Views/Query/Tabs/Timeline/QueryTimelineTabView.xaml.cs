using System;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Timeline;

/// <summary>Dock document hosting the event timeline</summary>
public sealed partial class QueryTimelineTabView : UserControl, IDocumentCommands, IDisposable
{
    private QueryViewModel? _subscribed;

    public QueryTimelineTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        EventTimeline.ScopeChanged += OnScopeChanged;
        EventTimeline.PlayheadTimeChanged += OnPlayheadTimeChanged;
        EventTimeline.PlanNodeSelected += OnPlanNodeSelected;
        EventTimeline.EventSelected += OnEventSelected;
        EventTimeline.EventDoubleClicked += OnEventDoubleClicked;
        EventTimeline.IndexOpenRequested += OnIndexOpenRequested;
        EventTimeline.ExecutionPlanRequested += OnExecutionPlanRequested;
        EventTimeline.TraceOpenRequested += OnTraceOpenRequested;
        EventTimeline.PlayStateChanged += OnPlayStateChanged;

        Loaded += (_, _) => AttachStructureResolver();
    }

    private void AttachStructureResolver()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var cache = App.GetService<ColumnstoreCache>();

        EventTimeline.ResolveStructure =
            page => ColumnstoreStructureText.Describe(cache.GetPageReads(viewModel.Database, page));
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public FrameworkElement CreateCommands() => EventTimeline.CreateTransport();

    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until
        // tracking stops
        Bindings.StopTracking();

        EventTimeline.ScopeChanged -= OnScopeChanged;
        EventTimeline.PlayheadTimeChanged -= OnPlayheadTimeChanged;
        EventTimeline.PlanNodeSelected -= OnPlanNodeSelected;
        EventTimeline.EventSelected -= OnEventSelected;
        EventTimeline.EventDoubleClicked -= OnEventDoubleClicked;
        EventTimeline.IndexOpenRequested -= OnIndexOpenRequested;
        EventTimeline.ExecutionPlanRequested -= OnExecutionPlanRequested;
        EventTimeline.TraceOpenRequested -= OnTraceOpenRequested;
        EventTimeline.PlayStateChanged -= OnPlayStateChanged;

        if (_subscribed is not null)
        {
            _subscribed.PlayheadMoveRequested -= OnPlayheadMoveRequested;
            _subscribed = null;
        }

        EventTimeline.Dispose();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_subscribed is not null)
        {
            _subscribed.PlayheadMoveRequested -= OnPlayheadMoveRequested;
        }

        _subscribed = args.NewValue as QueryViewModel;

        if (_subscribed is not null)
        {
            _subscribed.PlayheadMoveRequested += OnPlayheadMoveRequested;
        }
    }

    private void OnPlayStateChanged(bool isPlaying)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsTimelinePlaying = isPlaying;
        }
    }

    private void OnScopeChanged(long fromUs, long toUs) => ViewModel?.SetScope(fromUs, toUs);

    private void OnPlayheadTimeChanged(long timeUs) => ViewModel?.SetPlayheadTime(timeUs);

    private void OnPlayheadMoveRequested(long timeUs) => EventTimeline.MovePlayheadTo(timeUs);

    private void OnExecutionPlanRequested(ExecutionOperatorEvent op)
    {
        if (op.PlanNodeIdentifier is { } identifier)
        {
            ViewModel?.OpenExecutionPlan(identifier);
        }
    }

    private void OnPlanNodeSelected(PlanNodeIdentifier identifier) => ViewModel?.SelectPlanNode(identifier);

    private void OnEventSelected(EngineEvent engineEvent) => ViewModel?.NavigateToEvent(engineEvent);

    private void OnEventDoubleClicked(EngineEvent engineEvent) => ViewModel?.OpenEventPage(engineEvent);

    private void OnIndexOpenRequested(ExecutionOperatorEvent op) => ViewModel?.OpenIndex(op);

    private void OnTraceOpenRequested(ExecutionOperatorEvent op)
    {
        if (op.PlanNodeIdentifier is { } identifier)
        {
            ViewModel?.OpenTrace(identifier);
        }
    }
}
