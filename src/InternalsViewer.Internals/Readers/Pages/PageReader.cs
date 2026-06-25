using System.Globalization;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Abstract class for reading pages from hex dump
/// </summary>
public abstract class PageReader
{
    protected static int ReadData(ReadOnlySpan<char> hexSection, int offset, byte[] data)
    {
        var i = 0;

        while (i < hexSection.Length && offset < PageData.Size)
        {
            if (hexSection[i] == ' ')
            {
                i++;
                continue;
            }

            if (i + 1 < hexSection.Length)
            {
                var pair = hexSection.Slice(i, 2);

                if (!pair.Contains('†') && !pair.Contains('.')
                    && byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out data[offset]))
                {
                    offset++;
                }
            }

            i += 2;
        }

        return offset;
    }
}