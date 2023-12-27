using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Helpers;

public class StringHelpers
{
    private static readonly char[] HexDigits =
    {
        '0', '1', '2', '3', '4', '5', '6', '7',
        '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
    };

    /// <summary>
    /// Returns a hex string for a given array of bytes
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns></returns>
    public static string ToHexString(byte[]? bytes)
    {
        if (bytes == null)
        {
            return string.Empty;
        }

        var chars = new char[bytes.Length * 2];

        for (var i = 0; i < bytes.Length; i++)
        {
            int b = bytes[i];

            chars[i * 2] = HexDigits[b >> 4];
            chars[i * 2 + 1] = HexDigits[b & 0xF];
        }

        return new string(chars);
    }

    /// <summary>
    /// Returns a hex string for a given byte
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns></returns>
    public static string ToHexString(byte bytes)
    {
        return ToHexString(new byte[1] { bytes });
    }
}
