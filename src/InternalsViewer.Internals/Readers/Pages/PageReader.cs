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

        while (i + 1 < hexSection.Length && offset < PageData.Size)
        {
            if (hexSection[i] == ' ')
            {
                i++;
                continue;
            }

            var high = HexValue(hexSection[i]);
            
            var low = HexValue(hexSection[i + 1]);

            if ((high | low) >= 0)
            {
                data[offset] = (byte)((high << 4) | low);

                offset++;
            }

            i += 2;
        }

        return offset;
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1,
    };
}
