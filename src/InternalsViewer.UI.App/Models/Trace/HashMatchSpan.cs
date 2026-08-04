using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record HashMatchSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public HashMatchProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class HashMatchProgress : ObservableObject
{
    [ObservableProperty]
    private int _matches;

    [ObservableProperty]
    private int _emits;

    [ObservableProperty]
    private int _bucket = -1;

    [ObservableProperty]
    private int _entry;

    [ObservableProperty]
    private int _pairNumber;

    public void Apply(AccessStep.HashCompare compare)
    {
        Matches++;

        Bucket = compare.Bucket;
        Entry = compare.Entry;
    }

    public void Apply(AccessStep.JoinEmit emit)
    {
        Emits++;

        PairNumber = emit.PairNumber;
    }
}
