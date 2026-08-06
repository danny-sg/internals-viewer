using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Hash;

/// <summary>
/// One column of the hash table grid, shared by the header and every cell beneath it
/// </summary>
public sealed partial class HashColumnModel : ObservableObject
{
    /// <summary>
    /// The two columns that exist before any row has been read, so the grid always has a header
    /// </summary>
    public static IReadOnlyList<HashColumnModel> CreateBaseColumns() =>
    [
        new() { Header = "Bucket", IsMonospace = true, Width = 62 },
        new() { Header = "Hash", IsMonospace = true, Width = 92 }
    ];

    public string Header { get; init; } = string.Empty;

    public bool IsMonospace { get; init; }

    [ObservableProperty]
    private double _width = 120;
}
