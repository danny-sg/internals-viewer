using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Views;

public sealed partial class QueryView : Page
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    public QueryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        EventTimeline.SequenceChanged += OnSequenceChanged;
        EventTimeline.PlayheadChanged += OnPlayheadChanged;
        EventTimeline.PlanNodeSelected += OnTimelinePlanNodeSelected;
        EventTimeline.EventSelected += OnTimelineEventSelected;
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
        ViewModel.NavigateToEvent(engineEvent);
    }
}
