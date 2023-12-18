using System.Collections;
using System.Text;

namespace InternalsViewer.Internals.Extensions;

public static class ByteExtensions
{
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
}
