using System.Collections.Generic;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Engine.Records.Compressed
{
    public class CompressedDataRecord : Record
    {
        public CompressedDataRecord(Page page, ushort slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            CdItems = new List<CdArrayItem>();

            CompressedDataRecordLoader.Load(this);
        }

        //public byte[] CdArray
        //{
        //    get { return cdArray; }
        //    set { cdArray = value; }
        //}

        public List<CdArrayItem> CdItems { get; }

        public CdArrayItem[] CdItemsArray => CdItems.ToArray();

        public byte GetCdByte(int columnId)
        {
            return CdItems[columnId / 2].Values[columnId % 2];
        }

        public short CompressedSize { get; set; }
    }
}
