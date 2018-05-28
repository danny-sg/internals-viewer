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

        public BlobRecord(Page page, UInt16 slot)
            : base(page, slot, null)
        {
            BlobRecordLoader.Load(this);
        }

        [MarkAttribute("Status Bits A", "Red", "Gainsboro", true)]
        public new string StatusBitsADescription
        {
            get { return "TODO"; }
        }

        [MarkAttribute("Status Bits B", "Maroon", "Gainsboro", true)]
        public string StatusBitsBDescription
        {
            get { return string.Empty; }
        }

        [MarkAttribute("ID", "Navy", "AliceBlue", true)]
        public long BlobId { get; set; }

        [MarkAttribute("Length", "Blue", "Gainsboro", true)]
        public int Length { get; set; }

        public BlobType BlobType { get; set; }

        [MarkAttribute("Type", "DarkGreen", "Gainsboro", true)]
        public string BlobTypeDescription
        {
            get { return BlobType.ToString(); }
        }

        [MarkAttribute("MaxLinks", "FireBrick", "Gainsboro", true)]
        public int MaxLinks { get; set; }

        [MarkAttribute("Current Links", "FireBrick", "Gainsboro", true)]
        public int CurLinks { get; set; }

        [MarkAttribute("Level", "SlateGray", "Gainsboro", true)]
        public short Level { get; set; }

        [MarkAttribute("Size", "Purple", "Gainsboro", true)]
        public short Size { get; set; }

        [MarkAttribute("Data", "Gray", "PaleGoldenrod", true)]
        public byte[] Data { get; set; }

        public List<BlobChildLink> BlobChildren { get; set; }

        [MarkAttribute("Data", "White", "White", false)]
        public BlobChildLink[] BlobChildrenArray
        {
            get { return BlobChildren.ToArray(); }
        }
        
        internal static string GetRecordType(RecordType recordType)
        {
            throw new NotImplementedException();
        }
    }
}
