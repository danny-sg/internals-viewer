using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

namespace InternalsViewer.Internals.Engine.Records.Blob;

public class BlobRecord : FixedVarRecord
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

    [DataStructureItem(ItemType.StatusBitsA)]
    public string StatusBitsADescription => "TODO";

    [DataStructureItem(ItemType.StatusBitsB)]
    public string StatusBitsBDescription => string.Empty;

    [DataStructureItem(ItemType.BlobId)]
    public long BlobId { get; set; }

    [DataStructureItem(ItemType.BlobLength)]
    public int Length { get; set; }

    public BlobType BlobType { get; set; }

    [DataStructureItem(ItemType.BlobType)]
    public string BlobTypeDescription => BlobType.ToString();

    [DataStructureItem(ItemType.MaxLinks)]
    public int MaxLinks { get; set; }

    [DataStructureItem(ItemType.CurrentLinks)]
    public int CurLinks { get; set; }

    [DataStructureItem(ItemType.IndexLevel)]
    public short Level { get; set; }

    [DataStructureItem(ItemType.BlobSize)]
    public short Size { get; set; }

    [DataStructureItem(ItemType.BlobData)]
    public byte[] Data { get; set; } = Array.Empty<byte>(); 

    public List<BlobChildLink> BlobChildren { get; set; } = new();

    public BlobChildLink[] BlobChildrenArray => BlobChildren.ToArray();

    internal static string GetRecordType(RecordType recordType)
    {
        throw new NotImplementedException();
    }
}