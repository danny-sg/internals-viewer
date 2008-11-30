using System;

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
        public static UInt16[] GetOffsetArray(byte[] record, int size, int offset)
        {
            UInt16[] offsetArray = new UInt16[size];

            for (int i = 0; i < size; i++)
            {
                offsetArray[i] = BitConverter.ToUInt16(record, offset);

                offset += sizeof(UInt16);
            }

            return offsetArray;
        }

        /// <summary>
        /// Flips the high bit if set
        /// </summary>
        /// <param name="offset">The value.</param>
        /// <returns></returns>
        public static UInt16 DecodeOffset(UInt16 value)
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
