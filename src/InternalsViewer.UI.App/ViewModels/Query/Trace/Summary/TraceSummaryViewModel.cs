using System;
using System.Collections.Generic;
using System.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public sealed class TraceSummaryViewModel
{
    public TraceSummaryViewModel(MemoryHistoryViewModel history, Func<ITraceNavigator> getNavigator)
    {
        Panes = [new TraceSummaryOperatorsViewModel(history, getNavigator) { IsExpanded = true },
                 new TraceSummaryMemoryViewModel(history, getNavigator)];

        foreach (var pane in Panes)
        {
            pane.PropertyChanged += OnPanePropertyChanged;
        }
    }

    public List<TraceSummaryPaneViewModel> Panes { get; }

    private bool IsVisible { get; set; }

    public void Show()
    {
        IsVisible = true;

        foreach (var pane in Panes)
        {
            if (pane.IsExpanded)
            {
                pane.Show();
            }
        }
    }

    public void Reset()
    {
        foreach (var pane in Panes)
        {
            pane.Reset();
        }
    }

    public void SetPlayhead(TracePosition position)
    {
        foreach (var pane in Panes)
        {
            pane.SetPlayhead(position);
        }
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TraceSummaryPaneViewModel.IsExpanded)
            || sender is not TraceSummaryPaneViewModel { IsExpanded: true } expanded)
        {
            return;
        }

        foreach (var pane in Panes)
        {
            if (!ReferenceEquals(pane, expanded))
            {
                pane.IsExpanded = false;
            }
        }

        if (IsVisible)
        {
            expanded.Show();
        }
    }
}
