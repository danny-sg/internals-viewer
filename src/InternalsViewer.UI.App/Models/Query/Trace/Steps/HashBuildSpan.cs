using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record HashBuildSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public HashBuildProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class HashBuildProgress : ObservableObject
{
    private int[] _fill = [];

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private int _bucket;

    [ObservableProperty]
    private uint _hash;

    [ObservableProperty]
    private int _chainLength;

    [ObservableProperty]
    private int _fillVersion;

    public IReadOnlyList<int> Fill => _fill;

    public void Apply(AccessStep.HashBuild build)
    {
        Count++;

        Bucket = build.Bucket;
        Hash = build.Hash;
        ChainLength = build.ChainLength;

        if (_fill.Length != build.BucketCount)
        {
            _fill = new int[build.BucketCount];

            OnPropertyChanged(nameof(Fill));
        }

        if (build.Bucket >= 0 && build.Bucket < _fill.Length)
        {
            _fill[build.Bucket] = build.ChainLength;
        }

        FillVersion++;
    }
}
