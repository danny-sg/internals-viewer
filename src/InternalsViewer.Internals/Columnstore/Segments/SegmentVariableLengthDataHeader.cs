using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

public sealed class SegmentVariableLengthDataHeader : DataStructure
{
    public const int Size = 12;

    public int Offset { get; set; }

    [DataStructureItem(ItemType.VariableLengthDataSubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.VariableLengthDataValueCount)]
    public int ValueCount { get; set; }

    [DataStructureItem(ItemType.VariableLengthDataMaxStringSize)]
    public int MaxStringSize { get; set; }

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(ValueCount), Offset + 0x04, 4);
        MarkProperty(nameof(MaxStringSize), Offset + 0x08, 4);
    }
}
