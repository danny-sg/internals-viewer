using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Ticks;

[Trait("Category", "Unit")]
public class TraceTickDefinitionTests
{
    [Fact]
    public void The_Predefined_Rules_Make_The_Built_In_Tracks_In_Order()
    {
        Assert.Equal(["GetRow", "Reads", "Evaluations", "Row Access", "Seek"],
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
    public void A_Rule_Without_A_Label_Is_Labelled_With_Its_Function()
    {
        var rule = new TraceTickDefinition("Compares", "SqlTsEs", "CEsExec::FastCompare1", "#FFFFFF", 1, 1, []);

        Assert.Equal("CEsExec::FastCompare1", rule.GetLabel());
    }
}
