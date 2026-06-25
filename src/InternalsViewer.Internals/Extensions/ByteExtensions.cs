using System.Text;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Extensions;

public static class ByteExtensions
{
    /// <summary>
    /// Extension method to return a binary/bitmap string for a given byte
    /// </summary>
    public static string ToBinaryString(this byte input)
    {
        return string.Create(8, input, static (span, b) =>
        {
            for (var i = 0; i < 8; i++)
            {
                span[i] = (b & (1 << i)) != 0 ? '1' : '0';
            }
        });
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
