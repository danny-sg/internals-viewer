using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers
{
    /// <summary>
    /// Row Overflow field
    /// </summary>
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

        public OverflowField(byte[] data, int offset)
            : base(data, offset)
        {
            this.Mark("Unused", offset + OverflowField.UnusedOffset, sizeof(byte));

            this.unused = data[UnusedOffset];

            this.Mark("Level", offset + OverflowField.LevelOffset, sizeof(byte));

            this.Level = data[LevelOffset];

            this.Mark("Timestamp", offset + OverflowField.LevelOffset, sizeof(Int32));

            this.Timestamp = BitConverter.ToInt32(data, TimestampOffset);

            this.Mark("UpdateSeq", offset + OverflowField.UpdateSeqOffset, sizeof(Int16));

            this.updateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);
        }

        protected override void LoadLinks()
        {
            this.Links = new List<BlobChildLink>();
            RowIdentifier rowId;
            byte[] rowIdData;

            this.length = BitConverter.ToInt32(Data, ChildOffset);

            rowIdData = new byte[8];
            Array.Copy(Data, ChildOffset + 4, rowIdData, 0, 8);

            this.Mark("LinksArray", string.Empty, 0);

            rowId = new RowIdentifier(rowIdData);

            BlobChildLink link = new BlobChildLink(rowId, this.Length, 0);

            link.Mark("RowIdentifier", this.Offset + ChildOffset + 4, 8);

            this.Links.Add(link);
        }

        /// <summary>
        /// Gets or sets the B-Tree level.
        /// </summary>
        /// <value>The level.</value>
        [MarkAttribute("Level", "Red", "PeachPuff", true)]
        public byte Level
        {
            get { return this.level; }
            set { this.level = value; }
        }

        /// <summary>
        /// Gets or sets the length.
        /// </summary>
        /// <value>The length.</value>
        [MarkAttribute("Length", "Red", "PeachPuff", true)]
        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        [MarkAttribute("Unused", "DarkGreen", "PeachPuff", true)]
        public byte Unused
        {
            get { return this.unused; }
            set { this.unused = value; }
        }

        /// <summary>
        /// Gets or sets the update seq (used by optomistic concurrency control for cursors)
        /// </summary>
        /// <value>The update seq.</value>
        [MarkAttribute("UpdateSeq", "DarkGreen", "PeachPuff", true)]
        public short UpdateSeq
        {
            get { return this.updateSeq; }
            set { this.updateSeq = value; }
        }
    }
}
