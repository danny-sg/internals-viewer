using System.Text;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryTextReaderTests
{
    [Fact]
    public void A_Block_Starting_With_Utf16_Text_Is_Labelled_By_The_Text()
        => Assert.Equal("\"TextField\"", MemoryHistoryTextReader.GetLabel(Image([.. Encoding.Unicode.GetBytes("TextField"), 0, 0, 7, 9])));

    [Fact]
    public void Text_Running_To_The_End_Of_What_Is_Known_Is_Still_Text()
        => Assert.Equal("\"dbo\"", MemoryHistoryTextReader.GetLabel(Image(Encoding.Unicode.GetBytes("dbo"), unknownTail: 4)));

    [Fact]
    public void A_Pointer_Is_Not_Text()
        => Assert.Null(MemoryHistoryTextReader.GetLabel(Image(BitConverter.GetBytes(0x0000_026F_0ABA_AC50UL))));

    [Fact]
    public void Fewer_Than_Three_Characters_Are_Not_Text()
        => Assert.Null(MemoryHistoryTextReader.GetLabel(Image([.. Encoding.Unicode.GetBytes("AB"), 0, 0])));

    [Fact]
    public void Long_Text_Is_Cut_And_Marked_As_Truncated()
    {
        var label = MemoryHistoryTextReader.GetLabel(Image(Encoding.Unicode.GetBytes(new string('x', 100))));

        Assert.Equal($"\"{new string('x', 64)}\" (Truncated)", label);
    }

    [Fact]
    public void The_Full_Text_Is_Read_Without_Quotes_Or_A_Length_Limit()
    {
        var text = $"SELECT TOP 100 * FROM {new string('x', 100)}";

        Assert.Equal(text, MemoryHistoryTextReader.GetText(Image(Encoding.Unicode.GetBytes(text))));
    }

    [Fact]
    public void A_Narrow_String_Literal_Is_Read_Up_To_Its_Terminator()
        => Assert.Equal("\"xstmtquery.cpp\"",
                        MemoryHistoryTextReader.GetLiteralLabel([.. Encoding.ASCII.GetBytes("xstmtquery.cpp"), 0, 0x41]));

    [Fact]
    public void A_Wide_String_Literal_Is_Read_As_Utf16()
        => Assert.Equal("\"sysname\"", MemoryHistoryTextReader.GetLiteralLabel([.. Encoding.Unicode.GetBytes("sysname"), 0, 0]));

    private static MemoryHistoryImage Image(byte[] data, int unknownTail = 0)
    {
        var known = Enumerable.Repeat(true, data.Length).Concat(Enumerable.Repeat(false, unknownTail)).ToArray();

        return new MemoryHistoryImage([.. data, .. new byte[unknownTail]], known, new bool[known.Length]);
    }
}
