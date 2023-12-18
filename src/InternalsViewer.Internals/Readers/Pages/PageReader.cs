using System.Globalization;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Abstract class for reading pages from hex dump
/// </summary>
public abstract class PageReader
{
    protected static int ReadData(string currentRow, int offset, byte[] data)
    {
        var currentData = currentRow.Substring(20, 44).Replace(" ", string.Empty);

        for (var i = 0; i < currentData.Length; i += 2)
        {
            var byteString = currentData.Substring(i, 2);

            if (!byteString.Contains("†") && !byteString.Contains(".") && offset < Page.Size)
            {
                if (byte.TryParse(byteString,
                                  NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture,
                                  out data[offset]))
                {
                    offset++;
                }
            }
        }

        return offset;
    }
}