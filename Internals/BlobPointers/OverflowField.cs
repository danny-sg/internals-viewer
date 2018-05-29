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

        public OverflowField(byte[] data, int offset)
            : base(data, offset)
        {
            Mark("Unused", offset + OverflowField.UnusedOffset, sizeof(byte));

            Unused = data[UnusedOffset];

            Mark("Level", offset + OverflowField.LevelOffset, sizeof(byte));

            Level = data[LevelOffset];

            Mark("Timestamp", offset + OverflowField.LevelOffset, sizeof(int));

            Timestamp = BitConverter.ToInt32(data, TimestampOffset);

            Mark("UpdateSeq", offset + OverflowField.UpdateSeqOffset, sizeof(short));

            UpdateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);
        }

        protected override void LoadLinks()
        {
            Links = new List<BlobChildLink>();
            RowIdentifier rowId;
            byte[] rowIdData;

            Length = BitConverter.ToInt32(Data, ChildOffset);

            rowIdData = new byte[8];
            Array.Copy(Data, ChildOffset + 4, rowIdData, 0, 8);

            Mark("LinksArray", string.Empty, 0);

            rowId = new RowIdentifier(rowIdData);

            var link = new BlobChildLink(rowId, Length, 0);

            link.Mark("RowIdentifier", Offset + ChildOffset + 4, 8);

            Links.Add(link);
        }

        /// <summary>
        /// Gets or sets the B-Tree level.
        /// </summary>
        /// <value>The level.</value>
        [Mark(MarkType.Level)]
        public byte Level { get; set; }

        /// <summary>
        /// Gets or sets the length.
        /// </summary>
        /// <value>The length.</value>
        [Mark(MarkType.OverflowLength)]
        public int Length { get; set; }

        [Mark(MarkType.Unused)]
        public byte Unused { get; set; }

        /// <summary>
        /// Gets or sets the update seq (used by optomistic concurrency control for cursors)
        /// </summary>
        /// <value>The update seq.</value>
        [Mark(MarkType.UpdateSeq)]
        public short UpdateSeq { get; set; }
    }
}
