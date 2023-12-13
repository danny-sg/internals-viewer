using System.Collections.Generic;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Records.Compressed;

public class CompressedDataRecord(Page page, ushort slotOffset, Structure structure) : Record(page, slotOffset, structure)
{
    public List<CdArray> CdItems { get; } = new();

    public CdArray[] CdItemsArray => CdItems.ToArray();

    public byte GetCdByte(int columnId)
    {
        return CdItems[columnId / 2].Values[columnId % 2];
    }

    public short CompressedSize { get; set; }
}