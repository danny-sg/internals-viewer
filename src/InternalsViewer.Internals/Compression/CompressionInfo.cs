using System.Collections;
using System.Text;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Records.Compressed;

namespace InternalsViewer.Internals.Compression;

public class CompressionInfo : DataStructure
{
    public static byte CiSize = 7;
    public static short Offset = 96;

    [DataStructureItem(DataStructureItemType.PageModCount)]
    public short PageModCount { get; set; }

    [DataStructureItem(DataStructureItemType.CiSize)]
    public short Size { get; set; }

    public BitArray StatusBits { get; set; } = new(0);

    public int SlotOffset { get; set; }

    [DataStructureItem(DataStructureItemType.StatusBitsA)]
    public string StatusDescription
    {
        get
        {
            var sb = new StringBuilder();

            if (HasAnchorRecord)
            {
                sb.Append("Has Anchor Record");
            }

            if (HasAnchorRecord && HasDictionary)
            {
                sb.Append(", ");
            }

            if (HasDictionary)
            {
                sb.Append("Has Dictionary");
            }

            return sb.ToString();
        }
    }
    public CompressedDataRecord? AnchorRecord { get; set; }

    public Dictionary? CompressionDictionary { get; set; }

    public bool HasAnchorRecord { get; set; }

    public bool HasDictionary { get; set; }

    [DataStructureItem(DataStructureItemType.CiLength)]
    public short Length { get; set; }
}