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

        private List<BlobChildLink> blobChildren;
        private long blobId;
        private BlobType blobType;
        private int curLinks;
        private byte[] data;
        private int length;
        private short level;
        private int maxLinks;
        private short size;

        public BlobRecord(Page page, UInt16 slot)
            : base(page, slot, null)
        {
            BlobRecordLoader.Load(this);
        }

        [MarkAttribute("Status Bits A", "Red", "Gainsboro", true)]
        public string StatusBitsADescription
        {
            get { return "TODO"; }
        }

        [MarkAttribute("Status Bits B", "Maroon", "Gainsboro", true)]
        public string StatusBitsBDescription
        {
            get { return string.Empty; }
        }

        [MarkAttribute("ID", "Navy", "AliceBlue", true)]
        public long BlobId
        {
            get { return this.blobId; }
            set { this.blobId = value; }
        }

        [MarkAttribute("Length", "Blue", "Gainsboro", true)]
        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        public BlobType BlobType
        {
            get { return this.blobType; }
            set { this.blobType = value; }
        }

        [MarkAttribute("Type", "DarkGreen", "Gainsboro", true)]
        public string BlobTypeDescription
        {
            get { return this.blobType.ToString(); }
        }

        [MarkAttribute("MaxLinks", "FireBrick", "Gainsboro", true)]
        public int MaxLinks
        {
            get { return this.maxLinks; }
            set { this.maxLinks = value; }
        }

        [MarkAttribute("Current Links", "FireBrick", "Gainsboro", true)]
        public int CurLinks
        {
            get { return this.curLinks; }
            set { this.curLinks = value; }
        }

        [MarkAttribute("Level", "SlateGray", "Gainsboro", true)]
        public short Level
        {
            get { return this.level; }
            set { this.level = value; }
        }

        [MarkAttribute("Size", "Purple", "Gainsboro", true)]
        public short Size
        {
            get { return this.size; }
            set { this.size = value; }
        }

        [MarkAttribute("Data", "Gray", "PaleGoldenrod", true)]
        public byte[] Data
        {
            get { return this.data; }
            set { this.data = value; }
        }

        public List<BlobChildLink> BlobChildren
        {
            get { return this.blobChildren; }
            set { this.blobChildren = value; }
        }

        [MarkAttribute("Data", "White", "White", false)]
        public BlobChildLink[] BlobChildrenArray
        {
            get { return this.BlobChildren.ToArray(); }
        }
        
        internal static string GetRecordType(RecordType recordType)
        {
            throw new NotImplementedException();
        }
    }
}
