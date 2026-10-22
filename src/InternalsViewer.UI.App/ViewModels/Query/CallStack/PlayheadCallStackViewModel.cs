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

    public override string EmptyText => "No Call Selected";

    protected override string CurrentText => "Innermost Selected Call";

    protected override bool ShowsMemory => true;

    private Action<IPlayheadChart>? ChartRequest { get; set; }

    public void Step(bool forward) => Chart?.SelectAdjacentCall(forward);

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

    public override void Zoom(PlayheadCallRow row) => Chart?.ZoomTo(row.Span);

    protected override void ShowCall(PlayheadCallRow row) => Query.NavigateToCall(row.Node, row.CallIndex);

    protected override void UseChart(Action<IPlayheadChart> action)
    {
        if (Chart is { } chart)
        {
            action(chart);
        }
    }
}
