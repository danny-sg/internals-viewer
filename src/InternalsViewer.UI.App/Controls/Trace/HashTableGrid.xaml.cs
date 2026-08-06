using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class HashTableGrid : UserControl
{
    public static readonly DependencyProperty BucketsProperty =
        DependencyProperty.Register(nameof(Buckets),
                                    typeof(IReadOnlyList<HashBucketModel>),
                                    typeof(HashTableGrid),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary),
                                    typeof(string),
                                    typeof(HashTableGrid),
                                    new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns),
                                    typeof(IReadOnlyList<HashColumnModel>),
                                    typeof(HashTableGrid),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty BucketCountProperty =
        DependencyProperty.Register(nameof(BucketCount),
                                    typeof(int),
                                    typeof(HashTableGrid),
                                    new PropertyMetadata(16));

    public HashTableGrid()
    {
        InitializeComponent();
    }

    public IReadOnlyList<int> BucketCountOptions { get; } = [4, 8, 16, 32, 64, 128, 256, 512];

    public IReadOnlyList<HashColumnModel>? Columns
    {
        get => (IReadOnlyList<HashColumnModel>?)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int BucketCount
    {
        get => (int)GetValue(BucketCountProperty);
        set => SetValue(BucketCountProperty, value);
    }

    public IReadOnlyList<HashBucketModel>? Buckets
    {
        get => (IReadOnlyList<HashBucketModel>?)GetValue(BucketsProperty);
        set => SetValue(BucketsProperty, value);
    }

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }
}
