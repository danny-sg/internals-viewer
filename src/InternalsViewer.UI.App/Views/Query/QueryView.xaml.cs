using System.ComponentModel;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views;

public sealed partial class QueryView : Page
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    // The height to restore the timeline row to when it is shown again. The grid splitter rewrites both
    // adjacent rows to fixed pixels when dragged, so this is captured (rather than a fixed "1*") to keep
    // the user's resized height across hide/show.
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
        EventTimeline.IndexOpenRequested += OnTimelineIndexOpenRequested;
        EventTimeline.PlayStateChanged += OnPlayStateChanged;

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = args.NewValue as QueryViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTimelineVisibility();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryViewModel.IsTimelineVisible))
        {
            ApplyTimelineVisibility();
        }
    }

    // Collapses/restores the timeline row in code (rather than binding its height) so the dock row can be
    // forced back to star on restore. The grid splitter converts both rows to fixed pixels when dragged;
    // left as a fixed pixel, the dock row would consume all the space and the restored timeline would get
    // (close to) zero height.
    private void ApplyTimelineVisibility()
    {
        if (ViewModel.IsTimelineVisible)
        {
            DockRow.Height = new GridLength(1, GridUnitType.Star);
            TimelineRow.Height = _savedTimelineHeight.Value > 0
                ? _savedTimelineHeight
                : new GridLength(1, GridUnitType.Star);
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

    private void OnTimelinePlanNodeSelected(PlanNodeIdentifier identifier)
    {
        ViewModel.SelectPlanNode(identifier);
    }

    private void OnTimelineEventSelected(EngineEvent engineEvent)
    {
        ViewModel.NavigateToEvent(engineEvent);
    }

    private void OnTimelineIndexOpenRequested(ExecutionOperatorEvent op)
    {
        ViewModel.OpenIndex(op);
    }
}
