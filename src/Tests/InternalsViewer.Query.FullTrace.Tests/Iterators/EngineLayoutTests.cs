using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Layouts;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStoreHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class EngineLayoutTests
{
    private const ulong Iterator = 0x1F4_0000_1000;

    private const ulong PlanObject = 0x1F4_0000_2000;

    private const string ResourceExtension = ".psv";

    [Theory]
    [InlineData(16, 0, 1210, 3, "SqlServer2022")]
    [InlineData(17, 0, 5015, 4, "SqlServer2025")]
    [InlineData(18, 0, 100, 1, "SqlServer2025")]
    public void A_Server_Version_Uses_The_Layout_Whose_Range_Holds_It(int major, int minor, int build, int revision, string expected)
        => Assert.Equal(expected, EngineLayout.Get(new Version(major, minor, build, revision)).Name);

    [Fact]
    public void An_Unknown_Version_Uses_The_Latest_Layout()
        => Assert.Same(EngineLayout.Latest, EngineLayout.Get(null));

    [Fact]
    public void Every_Layout_File_Has_The_Same_Fields()
    {
        var layouts = ReadResources().ToDictionary(r => r.Name, r => GetKeys(r.Text));

        var expected = layouts.Values.First();

        Assert.All(layouts, l => Assert.Equal(expected, l.Value));
    }

    [Fact]
    public void A_Version_Range_With_Wildcards_Covers_Every_Build_In_It()
    {
        var text = ReadResources().Single(r => r.Name.EndsWith("SqlServer2025.psv", StringComparison.Ordinal)).Text;

        var lines = text.ReplaceLineEndings("\n").Split('\n');

        lines[0] = "Version|16.0.4100.0-16.0.*";

        var layout = EngineLayoutReader.Read("Test", new StringReader(string.Join('\n', lines)));

        Assert.Equal(new Version(16, 0, 4100, 0), layout.MinimumVersion);
        Assert.Equal(new Version(16, 0, int.MaxValue, int.MaxValue), layout.MaximumVersion);
    }

    [Fact]
    public void A_Layout_File_Without_A_Version_Is_Refused()
    {
        var reader = new StringReader("Type|Name|Offset|Length|Kind|Writers");

        Assert.Throws<InvalidDataException>(() => EngineLayoutReader.Read("Test", reader));
    }

    [Fact]
    public void A_Trace_Without_A_Version_Uses_The_Layout_Where_Its_Iterators_Hold_Their_Plan_Objects()
    {
        var store = Build([Block(Iterator, 0x100)], [(2, Iterator + 0x48, BitConverter.GetBytes(PlanObject))]);

        var layout = EngineLayout.Find(store, [new IteratorPlanNode(Iterator, PlanObject, 0, 0, 0)]);

        Assert.Equal("SqlServer2022", layout.Name);
    }

    [Fact]
    public void A_Trace_With_No_Iterator_Memory_Uses_The_Latest_Layout()
        => Assert.Same(EngineLayout.Latest, EngineLayout.Find(Build([], []), [new IteratorPlanNode(Iterator, PlanObject, 0, 0, 0)]));

    private static IEnumerable<(string Name, string Text)> ReadResources()
    {
        var assembly = typeof(EngineLayout).Assembly;

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(ResourceExtension, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);

            yield return (name, reader.ReadToEnd());
        }
    }

    private static HashSet<(string Type, string Name)> GetKeys(string text)
        => [.. text.ReplaceLineEndings("\n")
                   .Split('\n')
                   .Select(l => l.Split('|'))
                   .Where(c => c.Length > 2 && c[0] != "Type")
                   .Select(c => (c[0], c[1]))];
}
