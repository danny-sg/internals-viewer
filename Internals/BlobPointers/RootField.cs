using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.BlobPointers
{
    public class RootField : BlobField
    {
        public const int ChildOffset = 12;
        public const short LevelOffset = 2;
        public const int TimestampOffset = 6;
        public const int UnusedOffset = 3;
        public const int UpdateSeqOffset = 4;
        private byte level;
        private int slotCount;
        private byte unused;
        private short updateSeq;

        public RootField(byte[] data)
            : base(data)
        {
            this.Unused = data[UnusedOffset];
            this.Level = data[LevelOffset];
            this.Timestamp = BitConverter.ToInt32(data, TimestampOffset);
            this.UpdateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);
        }

        protected override void LoadLinks()
        {
            Links = new Collection<BlobChildLink>();

            this.SlotCount = (Data.Length - 12) / 12;

            for (int i = 0; i < this.SlotCount; i++)
            {
                int length = BitConverter.ToInt32(Data, ChildOffset + (i * 12));

                byte[] rowIdData = new byte[8];
                Array.Copy(Data, ChildOffset + (i * 12) + 4, rowIdData, 0, 8);

                RowIdentifier rowId = new RowIdentifier(rowIdData);

                Links.Add(new BlobChildLink(rowId, length, 0));
            }
        }

        public int SlotCount
        {
            get { return this.slotCount; }
            set { this.slotCount = value; }
        }

        public byte Level
        {
            get { return this.level; }
            set { this.level = value; }
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
