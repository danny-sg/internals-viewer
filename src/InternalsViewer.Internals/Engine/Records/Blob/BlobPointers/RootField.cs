﻿using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

public class RootField : BlobField
{
    public const int ChildOffset = 12;
    public const short LevelOffset = 2;
    public const int TimestampOffset = 6;
    public const int UnusedOffset = 3;
    public const int UpdateSeqOffset = 4;

    public RootField(byte[] data, int offset)
        : base(data, offset)
    {
        Unused = data[UnusedOffset];

        MarkDataStructure("Unused", offset + UnusedOffset, sizeof(byte));

        Level = data[LevelOffset];

        MarkDataStructure("Level", offset + LevelOffset, sizeof(byte));

        Timestamp = BitConverter.ToInt32(data, TimestampOffset);

        MarkDataStructure("Timestamp", offset + TimestampOffset, sizeof(int));

        UpdateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);

        MarkDataStructure("UpdateSeq", offset + UpdateSeqOffset, sizeof(short));
    }

    protected override void LoadLinks()
    {
        Links = new List<BlobChildLink>();

        SlotCount = (Data.Length - 12) / 12;

        for (var i = 0; i < SlotCount; i++)
        {
            MarkDataStructure("LinksArray", "Child " + i + " - ", i);

            var length = BitConverter.ToInt32(Data, ChildOffset + i * 12);

            var rowIdData = new byte[8];
            Array.Copy(Data, ChildOffset + i * 12 + 4, rowIdData, 0, 8);

            var rowId = new RowIdentifier(rowIdData);

            var link = new BlobChildLink(rowId, 0, length);

            link.MarkDataStructure("Length", Offset + ChildOffset + i * 12, sizeof(int));

            link.MarkDataStructure("RowIdentifier", Offset + ChildOffset + i * 12 + sizeof(int), 8);

            Links.Add(link);
        }
    }

    [DataStructureItem(DataStructureItemType.SlotCount)]
    public int SlotCount { get; set; }

    [DataStructureItem(DataStructureItemType.Level)]
    public byte Level { get; set; }

    [DataStructureItem(DataStructureItemType.Unused)]
    public byte Unused { get; set; }

    [DataStructureItem(DataStructureItemType.UpdateSeq)]
    public short UpdateSeq { get; set; }
}