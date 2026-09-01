using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record AggregatePushdownSpan() : AccessStep(AccessPhase.Accumulate), ITraceSpan
{
    public AggregatePushdownProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class AggregatePushdownProgress : ObservableObject
{
    [ObservableProperty]
    private long _runs;

    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _rowsProbed;

    [ObservableProperty]
    private bool _hasProbed;

    [ObservableProperty]
    private long _groups;

    [ObservableProperty]
    private int _rowGroupId;

    public void Apply(AccessStep.AggregatePushdown step)
    {
        Rows += step.RowCount;

        Groups = step.Groups;

        RowGroupId = step.RowGroupId;

        if (step.IsRunFolded)
        {
            Runs++;

            return;
        }

        RowsProbed += step.RowCount;

        HasProbed = true;
    }
}
