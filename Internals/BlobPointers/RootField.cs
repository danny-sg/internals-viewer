using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;

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

        public RootField(byte[] data, int offset)
            : base(data, offset)
        {
            this.Unused = data[UnusedOffset];

            this.Mark("Unused", offset + UnusedOffset, sizeof(byte));

            this.Level = data[LevelOffset];

            this.Mark("Level", offset + RootField.LevelOffset, sizeof(byte));

            this.Timestamp = BitConverter.ToInt32(data, TimestampOffset);

            this.Mark("Timestamp", offset + RootField.TimestampOffset, sizeof(Int32));

            this.UpdateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);

            this.Mark("UpdateSeq", offset + RootField.UpdateSeqOffset, sizeof(Int16));
        }

        protected override void LoadLinks()
        {
            Links = new List<BlobChildLink>();

            this.SlotCount = (Data.Length - 12) / 12;

            for (int i = 0; i < this.SlotCount; i++)
            {
                this.Mark("LinksArray", "Child " + i + " - ", i);

                int length = BitConverter.ToInt32(Data, ChildOffset + (i * 12));

                byte[] rowIdData = new byte[8];
                Array.Copy(Data, ChildOffset + (i * 12) + 4, rowIdData, 0, 8);

                RowIdentifier rowId = new RowIdentifier(rowIdData);

                BlobChildLink link = new BlobChildLink(rowId, 0, length);

                link.Mark("Length", this.Offset + RootField.ChildOffset + (i * 12), sizeof(Int32));

                link.Mark("RowIdentifier", this.Offset + RootField.ChildOffset + (i * 12) + sizeof(Int32), 8);

                Links.Add(link);
            }
        }

        [MarkAttribute("Slot Count", "DarkGreen", "PeachPuff", true)]
        public int SlotCount
        {
            get { return this.slotCount; }
            set { this.slotCount = value; }
        }

        [MarkAttribute("Level", "Red", "PeachPuff", true)]
        public byte Level
        {
            get { return this.level; }
            set { this.level = value; }
        }

        [MarkAttribute("Unused", "DarkGreen", "PeachPuff", true)]
        public byte Unused
        {
            get { return this.unused; }
            set { this.unused = value; }
        }

        [MarkAttribute("UpdateSeq", "DarkGreen", "PeachPuff", true)]
        public short UpdateSeq
        {
            get { return this.updateSeq; }
            set { this.updateSeq = value; }
        }
    }
}
