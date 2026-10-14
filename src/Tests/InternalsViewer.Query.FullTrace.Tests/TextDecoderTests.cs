using System.Text;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TextDecoderTests
{
    [Fact]
    public void A_Narrow_String_Ends_At_Its_Terminator()
    {
        var text = TextDecoder.Decode(Parts(Encoding.Latin1.GetBytes("dbo.Orders\0garbage")), isWide: false, out var isTruncated);

        Assert.Equal(("dbo.Orders", false), (text, isTruncated));
    }

    [Fact]
    public void A_Wide_String_Ends_At_A_Two_Byte_Terminator()
    {
        var text = TextDecoder.Decode(Parts(Encoding.Unicode.GetBytes("Orders\0x")), isWide: true, out var isTruncated);

        Assert.Equal(("Orders", false), (text, isTruncated));
    }

    [Fact]
    public void A_String_With_No_Terminator_Is_Truncated()
    {
        var text = TextDecoder.Decode(Parts(Encoding.Latin1.GetBytes("abcdefgh")), isWide: false, out var isTruncated);

        Assert.Equal(("abcdefgh", true), (text, isTruncated));
    }

    [Fact]
    public void A_Missing_Part_Ends_The_String()
    {
        var parts = Parts(Encoding.Latin1.GetBytes("abcdefghijklmnop\0"));

        parts.Remove(1);

        var text = TextDecoder.Decode(parts, isWide: false, out var isTruncated);

        Assert.Equal(("abcdefgh", true), (text, isTruncated));
    }

    internal static SortedList<uint, ulong> Parts(byte[] bytes)
    {
        var padded = new byte[(bytes.Length + 7) / 8 * 8];

        bytes.CopyTo(padded, 0);

        var parts = new SortedList<uint, ulong>();

        for (var part = 0; part < padded.Length / 8; part++)
        {
            parts[(uint)part] = BitConverter.ToUInt64(padded, part * 8);
        }

        return parts;
    }
}
