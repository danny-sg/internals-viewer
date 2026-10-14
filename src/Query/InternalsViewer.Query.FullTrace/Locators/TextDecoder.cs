using System.Text;

namespace InternalsViewer.Query.FullTrace.Locators;

internal static class TextDecoder
{
    public static string Decode(SortedList<uint, ulong> parts, bool isWide, out bool isTruncated)
    {
        var bytes = new List<byte>(parts.Count * sizeof(ulong));

        var expected = 0u;

        foreach (var (part, value) in parts)
        {
            if (part != expected)
            {
                break;
            }

            bytes.AddRange(BitConverter.GetBytes(value));

            expected++;
        }

        var width = isWide ? 2 : 1;

        var end = -1;

        for (var index = 0; index + width <= bytes.Count; index += width)
        {
            if (bytes[index] == 0 && (width == 1 || bytes[index + 1] == 0))
            {
                end = index;

                break;
            }
        }

        isTruncated = end < 0;

        var length = isTruncated ? bytes.Count - bytes.Count % width : end;

        var span = bytes.ToArray().AsSpan(0, length);

        return isWide ? Encoding.Unicode.GetString(span) : Encoding.Latin1.GetString(span);
    }
}
