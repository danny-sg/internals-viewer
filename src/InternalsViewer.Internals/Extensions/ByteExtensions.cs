using InternalsViewer.Internals.Helpers;
using System.Collections;
using System.Globalization;
using System.Text;

namespace InternalsViewer.Internals.Extensions;

public static class ByteExtensions
{
    /// <summary>
    /// Extension method to return a binary/bitmap string for a given byte
    /// </summary>
    public static string ToBinaryString(this byte input)
    {
        var bitArray = new BitArray(new[] { input });

        var stringBuilder = new StringBuilder();

        for (var i = 0; i < bitArray.Length; i++)
        {
            stringBuilder.Append(bitArray[i] ? "1" : "0");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Extension method to return a hex string for a given byte
    /// </summary>
    public static string ToHexString(this byte input)
    {
        return StringHelpers.ToHexString(input);
    }

    /// <summary>
    /// Extension method to return a hex string for a given array of bytes
    /// </summary>
    public static string ToHexString(this byte[] input)
    {
        return StringHelpers.ToHexString(input);
    }
}
