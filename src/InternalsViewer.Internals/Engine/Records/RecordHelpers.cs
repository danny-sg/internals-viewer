using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Engine.Records;

public static class RecordHelpers
{
    /// <summary>
    /// Flips the high bit if set
    /// </summary>
    public static ushort DecodeOffset(ushort value)
    {
        if ((value | 0x8000) == value)
        {
            return Convert.ToUInt16(value ^ 0x8000);
        }

        return value;
    }

    /// <summary>
    /// Get an offset array from a record
    /// </summary>
    /// <returns>An array of 2-byte integers representing a start offset in the page</returns>
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

    public static string GetArrayString(ushort[] array)
    {
        var sb = new StringBuilder();

        foreach (var offset in array)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.AppendFormat("{0} - 0x{0:X}", offset);
        }

        return sb.ToString();
    }
}
