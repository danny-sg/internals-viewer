using System;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record MergeCompareSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public MergeCompareProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class MergeCompareProgress : ObservableObject
{
    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _directionText = "";

    [ObservableProperty]
    private AccessKey _movedFrom;

    [ObservableProperty]
    private AccessKey _movedTo;

    [ObservableProperty]
    private AccessKey _staticKey;

    public int Direction { get; private set; }

    public void Apply(AccessStep.MergeCompare compare)
    {
        var direction = Math.Sign(compare.Comparison);

        if (Count == 0)
        {
            Direction = direction;

            DirectionText = direction < 0 ? "Advance Outer" : "Advance Inner";

            MovedFrom = direction < 0 ? compare.OuterKey : compare.InnerKey;
        }

        Count++;

        MovedTo = direction < 0 ? compare.OuterKey : compare.InnerKey;

        StaticKey = direction < 0 ? compare.InnerKey : compare.OuterKey;
    }
}
