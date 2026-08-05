using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record MergeMatchSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public MergeMatchProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class MergeMatchProgress : ObservableObject
{
    [ObservableProperty]
    private int _matches;

    [ObservableProperty]
    private int _emits;

    [ObservableProperty]
    private AccessKey _key;

    [ObservableProperty]
    private int _pairNumber;

    public void Apply(AccessStep.MergeCompare compare)
    {
        Matches++;

        Key = compare.OuterKey;
    }

    public void Apply(AccessStep.JoinEmit emit)
    {
        Emits++;

        PairNumber = emit.PairNumber;
    }
}
