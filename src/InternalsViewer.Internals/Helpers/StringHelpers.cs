using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InternalsViewer.Internals.Helpers;

public static class StringHelpers
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

    public static string SplitCamelCase(this string value, string separator = " ")
    {
        return Regex.Replace(value, "(\\B[A-Z])", $"{separator}$1", RegexOptions.Compiled).Trim();
    }

    public static byte[] ToByteArray(this string value)
    {
        value = value.Replace(" ", string.Empty);

        var data = new byte[value.Length / 2];

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = byte.Parse(value.Substring(i * 2, 2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        return data;
    }

    /// <summary>
    /// Gets a string representation of a bit array
    /// </summary>
    public static string GetBitArrayString(BitArray nullBitmap)
    {
        var stringBuilder = new StringBuilder();

        for (var i = 0; i < nullBitmap.Length; i++)
        {
            stringBuilder.Insert(0, nullBitmap[i] ? "1" : "0");
        }

        return stringBuilder.ToString();
    }

    public static string CleanHex(this string value)
    {
        return Regex.Replace(value, "[^a-fA-F0-9]", string.Empty);
    }

    public static string GetArrayString<T>(IEnumerable<T> values)
    {
        return string.Join(", ", values);
    }
}
