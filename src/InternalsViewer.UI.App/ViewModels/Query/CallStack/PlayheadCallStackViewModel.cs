using System;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed class PlayheadCallStackViewModel(QueryViewModel query) : CallStackViewModel(query)
{
    public IPlayheadChart? Chart
    {
        get;
        set
        {
            field = value;

            if (value is not null && ChartRequest is { } request)
            {
                ChartRequest = null;

                request(value);
            }
        }
    }

    public override string EmptyText => "No Calls At The Playhead";

    protected override string CurrentText => "Running At The Playhead";

    private Action<IPlayheadChart>? ChartRequest { get; set; }

    public void Step(bool forward)
    {
        ClearSelection();

        Chart?.StepPlayhead(forward);
    }

    public void RequestChart(Action<IPlayheadChart> request)
    {
        Query.Layout.ShowTraceTimeline();

        if (Chart is { } chart)
        {
            request(chart);
        }
        else
        {
            ChartRequest = request;
        }
    }

    public override void Zoom(PlayheadCallRow row) => Chart?.ZoomTo(row.Start, row.End);

    protected override void ShowCall(PlayheadCallRow row) => Query.NavigateToCall(row.Node, row.Call);

    protected override void UseChart(Action<IPlayheadChart> action)
    {
        if (Chart is { } chart)
        {
            action(chart);
        }
    }
}
