using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
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
        1 or 4 
            => "Hash Table",
        3 
            => "String Store",
        _ => string.Empty
    };

    public int LastId => Dictionary.LastId;

    public long EntryCount => Dictionary.EntryCount;

    /// <summary>
    /// Data id the first entry answers to, the ids running to Last Id rather than starting at zero
    /// </summary>
    public long FirstId => Dictionary.LastId - Dictionary.EntryCount + 1;

    public long OnDiskSize => Dictionary.OnDiskSize;

    public LobPointer DataPointer => Dictionary.DataPointer;

    public PageAddress DataPage => DataPointer.PageAddress;

    public ushort DataSlot => (ushort)DataPointer.Slot;

    public bool HasDataPointer => !DataPointer.IsEmpty;

    public string DataPointerDescription
        => HasDataPointer ? $"({DataPage.FileId}:{DataPage.PageId}:{DataSlot})" : string.Empty;

    [ObservableProperty]
    private int _pageCount;
}
