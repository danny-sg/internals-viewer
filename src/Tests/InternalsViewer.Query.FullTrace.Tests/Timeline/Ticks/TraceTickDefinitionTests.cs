using System.Text;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Ticks;

[Trait("Category", "Unit")]
public class TraceTickDefinitionTests
{
    [Fact]
    public void The_Predefined_Rules_Make_The_Built_In_Tracks_In_Order()
    {
        Assert.Equal(["GetRow", "Reads", "Evaluations", "Row Access", "Seek", "Hash Probe"],
                     TraceTickDefinition.GetPredefined().Select(d => d.Name).Distinct());
    }

    [Fact]
    public void A_Rule_Keeps_Its_Label_Source_And_Value_Through_The_File_Format()
    {
        var rule = new TraceTickDefinition("Seek", string.Empty, string.Empty, "#4472C4", 1, 1, [])
        {
            Label = "Leaf Page",
            Source = TraceTickSourceType.Seek,
            Value = 3
        };

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTickDefinition.Read(stream));

        Assert.Equal(("Leaf Page", TraceTickSourceType.Seek, (byte)3), (read.Label, read.Source, read.Value));
    }

    [Fact]
    public void A_Rule_Keeps_Its_Plan_Part_Through_The_File_Format()
    {
        var rule = new TraceTickDefinition("Seek", string.Empty, string.Empty, "#4472C4", 1, 1, []) { PlanPart = "Index Seek.Root Node" };

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule]);

        stream.Position = 0;

        Assert.Equal("Index Seek.Root Node", Assert.Single(TraceTickDefinition.Read(stream)).PlanPart);
    }

    [Fact]
    public void A_Rule_Keeps_Its_Background_Through_The_File_Format()
    {
        var rule = new TraceTickDefinition("Row Access", string.Empty, string.Empty, "Category", 1, 1, []) { Background = "#FFFFFF" };

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule]);

        stream.Position = 0;

        Assert.Equal("#FFFFFF", Assert.Single(TraceTickDefinition.Read(stream)).Background);
    }

    [Fact]
    public void The_Row_Access_Track_Has_A_White_Background()
    {
        Assert.Equal("#FFFFFF", TraceTickDefinition.GetPredefined().Single(d => d.Name == "Row Access").Background);
    }

    [Fact]
    public void Only_A_Counter_Rule_Writes_Its_Counter()
    {
        var rule = new TraceTickDefinition("GetRow", string.Empty, string.Empty, "#00B050", 1, 1, []);

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule with { IsCounter = true }, rule]);

        var text = Encoding.UTF8.GetString(stream.ToArray());

        stream.Position = 0;

        var read = TraceTickDefinition.Read(stream);

        Assert.Equal((1, true, false), (text.Split("IsCounter").Length - 1, read[0].IsCounter, read[1].IsCounter));
    }

    [Fact]
    public void A_Rule_Without_A_Label_Is_Labelled_With_Its_Function()
    {
        var rule = new TraceTickDefinition("Compares", "SqlTsEs", "CEsExec::FastCompare1", "#FFFFFF", 1, 1, []);

        Assert.Equal("CEsExec::FastCompare1", rule.GetLabel());
    }
}
