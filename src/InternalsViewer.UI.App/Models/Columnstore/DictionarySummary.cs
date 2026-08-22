using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Controls.Columnstore;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One dictionary as the metadata grid lists it
/// </summary>
public sealed partial class DictionarySummary : ObservableObject
{
    public required SegmentDictionary Dictionary { get; init; }

    public required string ColumnName { get; init; }

    public int DictionaryId => Dictionary.DictionaryId;

    public int ColumnId => Dictionary.ColumnId;

    public string Scope => Dictionary.IsGlobal ? "Global" : "Local";

    public string TypeDescription => $"{ColumnstoreLayout.GetDictionaryTypeDescription(Dictionary.Type)} Dictionary";

    /// <summary>
    /// The store the lob type opens, which the dictionary type decides
    /// </summary>
    public string SubTypeDescription => Dictionary.Type switch
    {
        1 => "Hash Table",
        3 => "String Store",
        _ => string.Empty
    };

    public int LastId => Dictionary.LastId;

    public long EntryCount => Dictionary.EntryCount;

    public long OnDiskSize => Dictionary.OnDiskSize;

    /// <summary>
    /// Pages the dictionary holds, which arrives with the header read rather than with the metadata
    /// </summary>
    [ObservableProperty]
    private int _pageCount;
}
