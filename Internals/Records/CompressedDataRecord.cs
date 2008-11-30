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
        private CompressionInformation compressionInfo;
        private byte[] cdArray;
        private short compressedSize;

        public CompressedDataRecord(Page page, UInt16 slotOffset, Structure structure, CompressionInformation compressionInfo)
            : base(page, slotOffset, structure)
        {
            this.CompressionInfo = compressionInfo;

            CompressedDataRecordLoader.Load(this);
        }

        public CompressionInformation CompressionInfo
        {
            get { return this.compressionInfo; }
            set { this.compressionInfo = value; }
        }

        public byte[] CdArray
        {
            get { return cdArray; }
            set { cdArray = value; }
        }

        public short CompressedSize
        {
            get { return compressedSize; }
            set { compressedSize = value; }
        }
    }
}
