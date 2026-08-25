using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Hash;

/// <summary>
/// One bucket of the hash table, with the build rows whose hash selected it
/// </summary>
public sealed partial class HashBucketModel : ObservableObject
{
    [ObservableProperty]
    private bool _isCurrent;

    public int Index { get; init; }

    public string IndexText => $"0x{Index:X2}";

    public BulkObservableCollection<HashEntryModel> Entries { get; } = [];
}
