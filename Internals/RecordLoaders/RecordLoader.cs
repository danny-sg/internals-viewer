using System;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.BlobPointers;

namespace InternalsViewer.Internals.RecordLoaders
{
    /// <summary>
    /// Loads a record
    /// </summary>
    public abstract class RecordLoader
    {
        /// <summary>
        /// Gets the variable offset array.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>An array of 2-byte integers</returns>
        public static ushort[] GetOffsetArray(byte[] record, int size, int offset)
        {
            var offsetArray = new ushort[size];

            for (var i = 0; i < size; i++)
            {
                offsetArray[i] = BitConverter.ToUInt16(record, offset);

                offset += sizeof(ushort);
            }

            return offsetArray;
        }

        /// <summary>
        /// Loads a LOB field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The data.</param>
        /// <param name="offset">The offset.</param>
        public static void LoadLobField(RecordField field, byte[] data, int offset)
        {
            field.Mark("BlobInlineRoot");

            // First byte gives the Blob field type
            switch ((BlobFieldType)data[0])
            {
                case BlobFieldType.LobPointer:

                    field.BlobInlineRoot = new PointerField(data, offset);
                    break;

                case BlobFieldType.LobRoot:

                    field.BlobInlineRoot = new RootField(data, offset);
                    break;

                case BlobFieldType.RowOverflow:

                    field.BlobInlineRoot = new OverflowField(data, offset);
                    break;
            }
        }

        /// <summary>
        /// Flips the high bit if set
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static ushort DecodeOffset(ushort value)
        {
            if ((value | 0x8000) == value)
            {
                return Convert.ToUInt16(value ^ 0x8000);
            }
            else
            {
                return value;
            }
        }
    }
}
