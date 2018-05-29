using System;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class SparseVector: Markable
    {
        public const int ColCountOffset = 2;
        public const int ColumnsOffset = 4;

        internal SparseVector(byte[] sparseRecord, TableStructure structure, DataRecord parentRecord, short recordOffset)
        {
            Data = sparseRecord;
            Structure = structure;
            ParentRecord = parentRecord;
            RecordOffset = recordOffset;

            SparseVectorLoader.Load(this);
        }

        public static string GetComplexHeaderDescription(short complexVector)
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

        public ushort[] Columns { get; set; }

        [Mark("Sparse Columns", "Black", "Olive")]
        public string ColumnsDescription => Record.GetArrayString(Columns);

        [Mark("Sparse Column Offsets", "Black", "DarkKhaki")]
        public string OffsetsDescription => Record.GetArrayString(Offset);

        public ushort[] Offset { get; set; }

        [Mark("Sparse Column Count", "Black", "SeaGreen")]
        public short ColCount { get; set; }

        public short RecordOffset { get; set; }

        public short ComplexHeader { get; set; }

        [Mark("Complex Header", "Green", "Gainsboro")]
        public string ComplexHeaderDescription => GetComplexHeaderDescription(ComplexHeader);
    }
}
