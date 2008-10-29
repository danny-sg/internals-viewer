using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;
using System.Collections.Generic;

namespace InternalsViewer.Internals.BlobPointers
{
    public class PointerField : BlobField
    {
        public const int RowIdOffset = 8;

        public PointerField(byte[] data, int offset)
            : base(data, offset)
        {
            this.Mark("Timestamp", this.Offset + sizeof(byte), sizeof(Int32));

            this.Timestamp = BitConverter.ToInt32(data, 0);
        }

        protected override void LoadLinks()
        {
            this.Links = new List<BlobChildLink>();

            byte[] rowIdData = new byte[8];
            Array.Copy(Data, RowIdOffset, rowIdData, 0, 8);

            this.Mark("LinksArray", string.Empty, 0);

            RowIdentifier rowId = new RowIdentifier(rowIdData);

            BlobChildLink link = new BlobChildLink(rowId, 0, 0);

            link.Mark("RowIdentifier", this.Offset + RowIdOffset, 8);

            this.Links.Add(link);
        }
    }
}
