using System;
using System.Collections.Generic;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
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

    public BlobRecord(Page page, ushort slot)
        : base(page, slot, null)
    {
        BlobRecordLoader.Load(this);
    }

    [Mark(MarkType.StatusBitsA)]
    public new string StatusBitsADescription => "TODO";

    [Mark(MarkType.StatusBitsB)]
    public string StatusBitsBDescription => string.Empty;

    [Mark(MarkType.BlobId)]
    public long BlobId { get; set; }

    [Mark(MarkType.BlobLength)]
    public int Length { get; set; }

    public BlobType BlobType { get; set; }

    [Mark(MarkType.BlobType)]
    public string BlobTypeDescription => BlobType.ToString();

    [Mark(MarkType.MaxLinks)]
    public int MaxLinks { get; set; }

    [Mark(MarkType.CurrentLinks)]
    public int CurLinks { get; set; }

    [Mark(MarkType.Level)]
    public short Level { get; set; }

    [Mark(MarkType.BlobSize)]
    public short Size { get; set; }

    [Mark(MarkType.BlobData)]
    public byte[] Data { get; set; }

    public List<BlobChildLink> BlobChildren { get; set; }

    public BlobChildLink[] BlobChildrenArray => BlobChildren.ToArray();

    internal static string GetRecordType(RecordType recordType)
    {
        throw new NotImplementedException();
    }
}