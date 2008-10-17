using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.BlobPointers
{
    public class OverflowField : BlobField
    {
        public const int ChildOffset = 12;
        public const int LevelOffset = 2;
        public const int TimestampOffset = 6;
        public const int UnusedOffset = 3;
        public const int UpdateSeqOffset = 4;

        private int length;
        private byte level;
        private byte unused;
        private short updateSeq;

        public OverflowField(byte[] data)
            : base(data)
        {
            this.unused = data[UnusedOffset];
            this.Level = data[LevelOffset];
            this.Timestamp = BitConverter.ToInt32(data, TimestampOffset);
            this.updateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);
        }

        protected override void LoadLinks()
        {
            this.Links = new Collection<BlobChildLink>();
            RowIdentifier rowId;
            byte[] rowIdData;

            this.length = BitConverter.ToInt32(Data, ChildOffset);

            rowIdData = new byte[8];
            Array.Copy(Data, ChildOffset + 4, rowIdData, 0, 8);

            rowId = new RowIdentifier(rowIdData);

            this.Links.Add(new BlobChildLink(rowId, this.Length, 0));
        }

        public byte Level
        {
            get { return this.level; }
            set { this.level = value; }
        }

        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        public byte Unused
        {
            get { return this.unused; }
            set { this.unused = value; }
        }

        public short UpdateSeq
        {
            get { return this.updateSeq; }
            set { this.updateSeq = value; }
        }
    }
}
