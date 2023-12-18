using System.Collections.Generic;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Engine.Records.Compressed;

public class CompressedDataRecord: Record
{
    public List<CdArray> CdItems { get; } = new();

    public CdArray[] CdItemsArray => CdItems.ToArray();

    public byte GetCdByte(int columnId)
    {
        return CdItems[columnId / 2].Values[columnId % 2];
    }

    public short CompressedSize { get; set; }
}