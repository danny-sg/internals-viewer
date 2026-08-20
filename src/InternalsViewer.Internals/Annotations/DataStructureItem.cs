namespace InternalsViewer.Internals.Annotations;

/// <summary>
/// Data Structure annotations that can be made against a <see cref="DataStructure">Data Structure</see> object
/// </summary>
public class DataStructureItem
{
    public ItemType ItemType { get; set; }

    public int Offset { get; set; } = -1;

    public int Length { get; set; } = -1;

    /// <summary>
    /// Bit offset from the start of the structure, or -1 when the item is byte aligned
    /// </summary>
    public int BitOffset { get; set; } = -1;

    /// <summary>
    /// Length in bits, or -1 when the item is byte aligned
    /// </summary>
    public int BitLength { get; set; } = -1;

    public bool IsBitAligned => BitOffset >= 0;

    public string Name { get; set; } = string.Empty;

    public int Index { get; set; } = -1;

    public string Prefix { get; set; } = string.Empty;

    public string[] Tags { get; set; } = [];

    public bool IsVisible { get; set; } = true;
}