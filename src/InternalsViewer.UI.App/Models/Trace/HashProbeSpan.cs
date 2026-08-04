using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;

namespace InternalsViewer.UI.App.Models.Trace;

public sealed record HashProbeSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public HashProbeProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class HashProbeProgress : ObservableObject
{
    [ObservableProperty]
    private int _rows;

    [ObservableProperty]
    private int _comparisons;

    [ObservableProperty]
    private int _matches;

    [ObservableProperty]
    private int _emits;

    [ObservableProperty]
    private int _bucket = -1;

    [ObservableProperty]
    private uint _hash;

    [ObservableProperty]
    private bool _isMatch;

    [ObservableProperty]
    private int _fillVersion;

    public IReadOnlyList<int>? Fill { get; set; }

    public void Apply(AccessStep.HashProbe probe)
    {
        Rows++;

        Bucket = probe.Bucket;
        Hash = probe.Hash;
        IsMatch = false;

        FillVersion++;
    }

    public void Apply(AccessStep.HashCompare compare)
    {
        Comparisons++;

        if (compare.IsMatch)
        {
            Matches++;

            IsMatch = true;
        }

        FillVersion++;
    }

    public void Apply(AccessStep.JoinEmit emit)
    {
        Emits++;
    }
}
