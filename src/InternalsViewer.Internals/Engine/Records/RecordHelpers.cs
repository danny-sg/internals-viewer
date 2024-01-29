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
