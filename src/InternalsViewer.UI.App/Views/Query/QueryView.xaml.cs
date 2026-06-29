using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views;

public sealed partial class QueryView : Page
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

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
