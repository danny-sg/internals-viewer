using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Hash;

/// <summary>
/// One row of the hash table grid, a build row held in a bucket
/// </summary>
public sealed partial class HashEntryModel : ObservableObject
{
    public IReadOnlyList<HashCellModel> Cells { get; init; } = [];

    [ObservableProperty]
    private bool _isMatched;

    [ObservableProperty]
    private bool _isCurrent;
}
