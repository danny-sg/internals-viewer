using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;

namespace InternalsViewer.Internals.Records
{
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

        [Mark("Status Bits A", "Red", "Gainsboro")]
        public new string StatusBitsADescription => "TODO";

        [Mark("Status Bits B", "Maroon", "Gainsboro")]
        public string StatusBitsBDescription => string.Empty;

        [Mark("ID", "Navy", "AliceBlue")]
        public long BlobId { get; set; }

        [Mark("Length", "Blue", "Gainsboro")]
        public int Length { get; set; }

        public BlobType BlobType { get; set; }

        [Mark("Type", "DarkGreen", "Gainsboro")]
        public string BlobTypeDescription => BlobType.ToString();

        [Mark("MaxLinks", "FireBrick", "Gainsboro")]
        public int MaxLinks { get; set; }

        [Mark("Current Links", "FireBrick", "Gainsboro")]
        public int CurLinks { get; set; }

        [Mark("Level", "SlateGray", "Gainsboro")]
        public short Level { get; set; }

        [Mark("Size", "Purple", "Gainsboro")]
        public short Size { get; set; }

        [Mark("Data", "Gray", "PaleGoldenrod")]
        public byte[] Data { get; set; }

        public List<BlobChildLink> BlobChildren { get; set; }

        [Mark("Data", "White", "White")]
        public BlobChildLink[] BlobChildrenArray => BlobChildren.ToArray();

        internal static string GetRecordType(RecordType recordType)
        {
            throw new NotImplementedException();
        }
    }
}
