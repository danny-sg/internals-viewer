using System;
using System.Collections.Generic;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records.Blob;

public class BlobRecord : Record
{
    public const short CurLinksOffset = 16;
    public const short DataOffset = 14;
    public const short IdOffset = 4;
    public const short InternalChildOffset = 20;
    public const short LengthOffset = 2;
    public const short MaxLinksOffset = 14;
    public const short RootChildOffset = 24;
    public const short RootLevelOffset = 18;
    public const short SmallDataOffset = 20;
    public const short SmallSizeOffset = 14;
    public const short TypeOffset = 12;

    [DataStructureItem(DataStructureItemType.StatusBitsA)]
    public new string StatusBitsADescription => "TODO";

    [DataStructureItem(DataStructureItemType.StatusBitsB)]
    public string StatusBitsBDescription => string.Empty;

    [DataStructureItem(DataStructureItemType.BlobId)]
    public long BlobId { get; set; }

    [DataStructureItem(DataStructureItemType.BlobLength)]
    public int Length { get; set; }

    public BlobType BlobType { get; set; }

    [DataStructureItem(DataStructureItemType.BlobType)]
    public string BlobTypeDescription => BlobType.ToString();

    [DataStructureItem(DataStructureItemType.MaxLinks)]
    public int MaxLinks { get; set; }

    [DataStructureItem(DataStructureItemType.CurrentLinks)]
    public int CurLinks { get; set; }

    [DataStructureItem(DataStructureItemType.Level)]
    public short Level { get; set; }

    [DataStructureItem(DataStructureItemType.BlobSize)]
    public short Size { get; set; }

    [DataStructureItem(DataStructureItemType.BlobData)]
    public byte[] Data { get; set; }

    public List<BlobChildLink> BlobChildren { get; set; }

    public BlobChildLink[] BlobChildrenArray => BlobChildren.ToArray();

    internal static string GetRecordType(RecordType recordType)
    {
        throw new NotImplementedException();
    }
}