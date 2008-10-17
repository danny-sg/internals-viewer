using System;
using System.Collections.ObjectModel;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.BlobPointers
{
    public class PointerField : BlobField
    {
        public const int RowIdOffset = 8;

        public PointerField(byte[] data)
            : base(data)
        {
            this.Timestamp = BitConverter.ToInt32(data, 0);
        }

        protected override void LoadLinks()
        {
            this.Links = new Collection<BlobChildLink>();

            byte[] rowIdData = new byte[8];
            Array.Copy(Data, RowIdOffset, rowIdData, 0, 8);

            RowIdentifier rowId = new RowIdentifier(rowIdData);

            this.Links.Add(new BlobChildLink(rowId, 0, 0));
        }
    }
}
