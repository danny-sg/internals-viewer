using System;
using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One handle, being where a string dictionary keeps the value an entry stands for
/// </summary>
/// <remarks>
/// A string is not fixed width, so it cannot be indexed the way a numeric dictionary indexes its value array.
/// The handle names the page holding the value and where it starts on that page, which is what the lookup follows.
/// </remarks>
public sealed class DictionaryHandleDetail(StringDictionary dictionary, int index) : IEquatable<DictionaryHandleDetail>
{
    public int Index { get; } = index;

    public long DataId => dictionary.FirstId + Index;

    public int Page => dictionary.Handles[Index].Page;

    /// <summary>
    /// Where the value starts on its page, counted in bits on a Huffman coded page and bytes on a plain one
    /// </summary>
    public int Offset => dictionary.Handles[Index].Offset;

    public int Length => StoredBytes?.Length ?? 0;

    /// <summary>
    /// Where the handle itself sits in the blob, the array running from a fixed place after the header
    /// </summary>
    public int HandleOffset => StringDictionary.HandleArrayOffset + (Index * dictionary.HandleSize);

    public string HandleOffsetDescription => $"0x{HandleOffset:X}";

    public bool IsLobPointer => dictionary.TryGetLobPointer(Index, out _);

    /// <summary>
    /// What the handle leads to, a value too big for the store leading to a pointer rather than the value
    /// </summary>
    public string Description => dictionary.TryGetLobPointer(Index, out var pointer)
        ? $"LOB {pointer.PageAddress} slot {pointer.Slot}, {pointer.Length} bytes"
        : string.Empty;

    private byte[]? StoredBytes => field ??= dictionary.GetValueBytesAt(Index);

    public bool Equals(DictionaryHandleDetail? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as DictionaryHandleDetail);

    public override int GetHashCode() => Index;
}