using System.Text;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Records.Compressed;

namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Compression Info record
/// </summary>
/// <remarks>
/// The CI record is a record added for pages compressed using the PAGE compression type.
/// 
/// It has the following structure:
/// 
///         - Header
///         - Page Modification Count
///         - Offsets (Length = CI Record Size, Size = CI Record Size)
///         - Anchor Record
///         - Dictionary
/// </remarks>
public class CompressionInfo : DataStructure
{
    [DataStructureItem(ItemType.PageModCount)]
    public short PageModificationCount { get; set; }

    [DataStructureItem(ItemType.CiSize)]
    public short Size { get; set; }

    [DataStructureItem(ItemType.CiLength)]
    public ushort Length { get; set; }

    [DataStructureItem(ItemType.CiHeader)]
    public byte Header { get; set; }

    public static ushort SlotOffset => 96;

    [DataStructureItem(ItemType.AnchorRecord)]
    public CompressedDataRecord? AnchorRecord { get; set; }

    [DataStructureItem(ItemType.CompressionDictionary)]
    public Dictionary? CompressionDictionary { get; set; }

    public bool HasAnchorRecord { get; set; }

    public bool HasDictionary { get; set; }
}