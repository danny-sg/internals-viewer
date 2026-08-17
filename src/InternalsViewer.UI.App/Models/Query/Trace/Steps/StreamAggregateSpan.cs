using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record StreamAggregateSpan() : AccessStep(AccessPhase.Accumulate), ITraceSpan
{
    public AggregateProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class AggregateProgress : ObservableObject
{
    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _groupRows;

    [ObservableProperty]
    private long _groups;

    [ObservableProperty]
    private string _running = "";

    [ObservableProperty]
    private string _detail = "";

    [ObservableProperty]
    private bool _isHashed;

    [ObservableProperty]
    private int _bucket = -1;

    [ObservableProperty]
    private int _fillVersion;

    private int[] _fill = [];

    public IReadOnlyList<int> Fill => _fill;

    public void Apply(AccessStep.AggregateRow step)
    {
        Rows = step.Number;
        GroupRows = step.GroupRows;
        Running = step.Running;

        Detail = Running;
    }

    public void Apply(AccessStep.HashAggregate step)
    {
        Rows = step.Number;
        GroupRows = step.GroupRows;
        Running = step.Running;

        if (step.IsNewGroup)
        {
            Groups++;
        }

        Detail = $"{Groups.ToString("N0", CultureInfo.InvariantCulture)} groups";

        IsHashed = true;
        Bucket = step.Bucket;

        if (_fill.Length != step.BucketCount)
        {
            _fill = new int[step.BucketCount];

            OnPropertyChanged(nameof(Fill));
        }

        if (step.Bucket >= 0 && step.Bucket < _fill.Length)
        {
            _fill[step.Bucket] = step.ChainLength;
        }

        FillVersion++;
    }
}
