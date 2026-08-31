using InternalsViewer.Query.Events.Parsers;
using InternalsViewer.Query.Events.Parsers.Xml;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class XmlCallStackParserTests
{
    // The callstack action is held in the event buffer as escaped XML: angle brackets are &lt;/&gt; while the
    // attribute quotes stay literal. The parser reads that form in place rather than decoding it to a string first.
    private const string ThreeFrames =
        """
        <frame id="00" address="0x7FFD81BD602C" pdb="sqlmin.pdb" age="2" guid="80B1767D-3B81-4D3F-BA30-30216965DE41" module="sqlmin" rva="0x1234"/>
        <frame id="01" address="0x00007FFD0000ABCD" pdb="sqllang.pdb" age="1" guid="AABBCCDD-0000-0000-0000-000000000000" module="sqllang" rva="4660"/>
        <frame id="02" address="0x00007FFD00001111" pdb="sqlmin.pdb" age="2" guid="80B1767D-3B81-4D3F-BA30-30216965DE41" module="sqlmin" rva="0x5678"/>
        """;

    [Fact]
    public void Reads_Frames_From_Escaped_Xml()
    {
        var frames = XmlCallStackParser.ParseCallStack(ThreeFrames, new StringInternPool());

        Assert.Equal(3, frames.Count);

        Assert.Equal("sqlmin", frames[0].Module);
        Assert.Equal("sqlmin.pdb", frames[0].Pdb);
        Assert.Equal("80B1767D-3B81-4D3F-BA30-30216965DE41", frames[0].Guid);
        Assert.Equal(2, frames[0].Age);
        Assert.Equal(0x7FFD81BD602CUL, frames[0].Address);

        Assert.Equal("sqllang", frames[1].Module);
    }

    [Fact]
    public void Parses_Hexadecimal_And_Decimal_Rva()
    {
        var frames = XmlCallStackParser.ParseCallStack(ThreeFrames, new StringInternPool());

        Assert.Equal(0x1234U, frames[0].Rva);
        Assert.Equal(4660U, frames[1].Rva);
    }

    [Fact]
    public void Interns_Repeated_Module_Pdb_And_Guid_To_One_Instance()
    {
        var frames = XmlCallStackParser.ParseCallStack(ThreeFrames, new StringInternPool());

        // Frames 0 and 2 share a module, pdb and guid, so the pool must hand back the same string instance rather
        // than a fresh allocation per frame.
        Assert.Same(frames[0].Module, frames[2].Module);
        Assert.Same(frames[0].Pdb, frames[2].Pdb);
        Assert.Same(frames[0].Guid, frames[2].Guid);
    }

    [Fact]
    public void Skips_Frames_Missing_A_Module()
    {
        const string encoded =
            """&lt;frame id="00" address="0x1" pdb="x.pdb" age="0" guid="00000000-0000-0000-0000-000000000000" rva="0x1"/&gt;""";

        var frames = XmlCallStackParser.ParseCallStack(encoded, new StringInternPool());

        Assert.Empty(frames);
    }

    [Fact]
    public void Returns_Empty_When_No_Frames_Present()
    {
        Assert.Empty(XmlCallStackParser.ParseCallStack(ReadOnlySpan<char>.Empty, new StringInternPool()));
        Assert.Empty(XmlCallStackParser.ParseCallStack("no frames here", new StringInternPool()));
    }
}
