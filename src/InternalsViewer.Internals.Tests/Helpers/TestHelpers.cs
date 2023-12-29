using System.Globalization;

namespace InternalsViewer.Internals.Tests.Helpers;

public static class TestHelpers
{
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
}
