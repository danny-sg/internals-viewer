using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Trace;

/// <summary>
/// One bucket of the hash table, with the build rows whose hash selected it
/// </summary>
public sealed partial class HashBucketModel : ObservableObject
{
    public int Index { get; init; }

    public string IndexText => $"0x{Index:X2}";

    public ObservableCollection<HashEntryModel> Entries { get; } = [];

    [ObservableProperty]
    private bool _isCurrent;
}
