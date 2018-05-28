using System;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.RecordLoaders
{
    class SparseVectorLoader
    {
        /// <summary>
        /// Loads a sparse vector.
        /// </summary>
        /// <param name="sparseVector">The sparse vector.</param>
        internal static void Load(SparseVector sparseVector)
        {
            int vectorOffset = sparseVector.ParentRecord.SlotOffset + sparseVector.RecordOffset;

            sparseVector.ComplexHeader = BitConverter.ToInt16(sparseVector.Data, 0);

            sparseVector.Mark("ComplexHeaderDescription", vectorOffset, sizeof(Int16));

            sparseVector.ColCount = BitConverter.ToInt16(sparseVector.Data, 2);

            sparseVector.Mark("ColCount", vectorOffset + SparseVector.ColCountOffset, sizeof(Int16));

            sparseVector.Columns = new UInt16[sparseVector.ColCount];

            sparseVector.Mark("ColumnsDescription", vectorOffset + SparseVector.ColumnsOffset, sparseVector.ColCount * sizeof(Int16));

            sparseVector.Offset = new UInt16[sparseVector.ColCount];

            sparseVector.Mark("OffsetsDescription",
                              vectorOffset + SparseVector.ColumnsOffset + sparseVector.ColCount * sizeof(Int16),
                              sparseVector.ColCount * sizeof(Int16));

            int previousOffset = 4 + (sparseVector.ColCount * 4);

            for (int i = 0; i < sparseVector.ColCount; i++)
            {
                sparseVector.Columns[i] = BitConverter.ToUInt16(sparseVector.Data, 4 + (i * 2));

                sparseVector.Offset[i] = BitConverter.ToUInt16(sparseVector.Data, (4 + sparseVector.ColCount * 2) + (i * 2));

                byte[] columnData = new byte[sparseVector.Offset[i] - previousOffset];

                Array.Copy(sparseVector.Data, previousOffset, columnData, 0, sparseVector.Offset[i] - previousOffset);

                Column column = sparseVector.Structure.Columns.Find(delegate(Column col) { return col.ColumnId == sparseVector.Columns[i]; });

                RecordField field = new RecordField(column);

                field.Data = columnData;
                field.Sparse = true;
                field.Offset = sparseVector.Offset[i];
                field.Length = sparseVector.Offset[i] - previousOffset;

                field.Mark("Value", vectorOffset + previousOffset, field.Length);

                sparseVector.ParentRecord.Mark("FieldsArray", field.Name + " (Sparse)", sparseVector.ParentRecord.Fields.Count);

                previousOffset = sparseVector.Offset[i];
                sparseVector.ParentRecord.Fields.Add(field);

            }
        }
    }
}
