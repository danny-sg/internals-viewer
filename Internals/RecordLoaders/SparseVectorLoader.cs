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
            Int16 colCount = sparseVector.ColCount;

            sparseVector.ComplexHeader = BitConverter.ToInt16(sparseVector.Data, 0);

            sparseVector.ColCount = BitConverter.ToInt16(sparseVector.Data, 2);

            sparseVector.Columns = new UInt16[colCount];
            sparseVector.Offset = new Int16[colCount];

            int previousOffset = 4 + (sparseVector.ColCount * 4);

            for (int i = 0; i < sparseVector.ColCount; i++)
            {
                sparseVector.Columns[i] = BitConverter.ToUInt16(sparseVector.Data, 4 + (i * 2));

                sparseVector.Offset[i] = BitConverter.ToInt16(sparseVector.Data, (4 + colCount * 2) + (i * 2));

                byte[] columnData = new byte[sparseVector.Offset[i] - previousOffset];

                Array.Copy(sparseVector.Data, previousOffset, columnData, 0, sparseVector.Offset[i] - previousOffset);

                Column column = sparseVector.Structure.Columns.Find(delegate(Column col) { return col.ColumnId == sparseVector.Columns[i]; });

                RecordField field = new RecordField(column);

                field.Data = columnData;
                field.Sparse = true;
                field.Offset = sparseVector.Offset[i];

                previousOffset = sparseVector.Offset[i];    
                
                sparseVector.ParentRecord.Fields.Add(field);

            }
        }
    }
}
