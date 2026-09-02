using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed partial class TraceFill : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBuckets))]
    private IReadOnlyList<int> buckets = [];

    [ObservableProperty]
    private int version;

    [ObservableProperty]
    private int bucket = -1;

    [ObservableProperty]
    private bool isMatch;

    public bool HasBuckets => Buckets.Count > 0;

    public void Set(IReadOnlyList<int> buckets)
    {
        Buckets = buckets;

        Version++;
    }

    public void Land(int bucket, int chainLength, int bucketCount)
    {
        if (bucketCount <= 0 || bucket < 0 || bucket >= bucketCount)
        {
            return;
        }

        if (Buckets is not int[] fill || fill.Length != bucketCount)
        {
            fill = new int[bucketCount];

            Buckets = fill;
        }

        fill[bucket] = chainLength;

        Bucket = bucket;

        Version++;
    }

    public void Touch(int bucket, bool isMatch)
    {
        Bucket = bucket;

        IsMatch = isMatch;

        Version++;
    }
}
