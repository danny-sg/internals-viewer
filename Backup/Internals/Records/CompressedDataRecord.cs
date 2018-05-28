using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Structures;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Records
{
    public class CompressedDataRecord : Record
    {
        private byte[] cdArray;
        private List<CdArrayItem> cdArrayItems;
        private short compressedSize;

        public CompressedDataRecord(Page page, UInt16 slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            this.cdArrayItems = new List<CdArrayItem>();

            CompressedDataRecordLoader.Load(this);
        }

        //public byte[] CdArray
        //{
        //    get { return cdArray; }
        //    set { cdArray = value; }
        //}

        public List<CdArrayItem> CdItems
        {
            get
            {
                return this.cdArrayItems;
            }
        }

        public CdArrayItem[] CdItemsArray
        {
            get
            {
                return this.cdArrayItems.ToArray();
            }
        }

        public byte GetCdByte(int columnId)
        {
            return this.CdItems[columnId / 2].Values[columnId % 2];
        }

        public short CompressedSize
        {
            get { return compressedSize; }
            set { compressedSize = value; }
        }
    }
}
