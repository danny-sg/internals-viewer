using System;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class SparseVector: Markable
    {
        public const int ColCountOffset = 2;
        public const int ColumnsOffset = 4;

        internal SparseVector(byte[] sparseRecord, TableStructure structure, DataRecord parentRecord, Int16 recordOffset)
        {
            Data = sparseRecord;
            Structure = structure;
            ParentRecord = parentRecord;
            RecordOffset = recordOffset;

            SparseVectorLoader.Load(this);
        }

        public static string GetComplexHeaderDescription(Int16 complexVector)
        {
            switch (complexVector)
            {
                case 5:
                    return "In row sparse vector";
                default:
                    return "Unknown";
            }
        }

        internal TableStructure Structure { get; set; }

        public byte[] Data { get; set; }

        internal DataRecord ParentRecord { get; set; }

        public UInt16[] Columns { get; set; }

        [MarkAttribute("Sparse Columns", "Black", "Olive", true)]
        public string ColumnsDescription
        {
            get { return Record.GetArrayString(Columns); }
        }

        [MarkAttribute("Sparse Column Offsets", "Black", "DarkKhaki", true)]
        public string OffsetsDescription
        {
            get { return Record.GetArrayString(Offset); }
        }

        public UInt16[] Offset { get; set; }

        [MarkAttribute("Sparse Column Count", "Black", "SeaGreen", true)]
        public Int16 ColCount { get; set; }

        public Int16 RecordOffset { get; set; }

        public Int16 ComplexHeader { get; set; }

        [MarkAttribute("Complex Header", "Green", "Gainsboro", true)]
        public string ComplexHeaderDescription
        {
            get { return GetComplexHeaderDescription(ComplexHeader); }
        }


    }
}
