using System.Text;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Ticks;

[Trait("Category", "Unit")]
public class TraceTickDefinitionTests
{
    [Fact]
    public void The_Predefined_Rules_Make_The_Built_In_Tracks_In_Order()
    {
        Assert.Equal(["GetRow", "Read", "Evaluations", "Row Access", "Seek", "Hash Table", "Sort", "Open"],
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
    public void A_Rule_Keeps_Its_Description_Through_The_File_Format()
    {
        var rule = new TraceTickDefinition("Reads", string.Empty, string.Empty, "#9B6BD6", 1, 1, [])
        {
            Description = "The PFS is used to check if an IAM extent is allocated"
        };

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTickDefinition.Read(stream));

        Assert.Equal("The PFS is used to check if an IAM extent is allocated", read.Description);
    }

    [Fact]
    public void A_Rule_Keeps_Its_Return_Description_Through_The_File_Format()
    {
        var rule = new TraceTickDefinition("GetRow", string.Empty, string.Empty, "#00B050", 1, 1, [])
        {
            ReturnDescription = "GetRow returns to parent operator"
        };

        using var stream = new MemoryStream();

        TraceTickDefinition.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTickDefinition.Read(stream));

        Assert.Equal("GetRow returns to parent operator", read.ReturnDescription);
    }

    [Fact]
    public void Every_GetRow_Rule_Describes_The_Call_And_The_Return()
    {
        var rules = TraceTickDefinition.GetPredefined().Where(d => d.Name == "GetRow").ToList();

        Assert.All(rules, r => Assert.Equal(("{ParentOperatorName} calls `GetRow` from {OperatorName}", true),
                                            (r.Description, r.ReturnDescription?.Contains("{OperatorName}") == true)));
    }

    [Fact]
    public void Every_Predefined_Rule_Has_A_Description()
    {
        Assert.All(TraceTickDefinition.GetPredefined(), r => Assert.False(string.IsNullOrEmpty(r.Description), r.GetLabel()));
    }

    [Fact]
    public void Placeholders_Are_Replaced_By_Name_And_Unknown_Ones_Are_Left()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OperatorName"] = "Table Scan",
            ["ParentOperatorName"] = "Top"
        };

        var text = TraceTickDefinition.Format("{ParentOperatorName} calls `GetRow` from {operatorname} at {Unknown}",
                                              n => values.GetValueOrDefault(n));

        Assert.Equal("Top calls `GetRow` from Table Scan at {Unknown}", text);
    }

    [Fact]
    public void The_Read_Track_Marks_IAM_And_PFS_Page_Fetches()
    {
        var fetches = TraceTickDefinition.GetPredefined().Where(d => d is { Name: "Read", Source: TraceTickSourceType.PageFetch });

        Assert.Equal([(byte)PageType.Iam, (byte)PageType.Pfs], fetches.Select(d => d.Value));
    }

    [Fact]
    public void The_Row_Access_Track_Has_A_White_Background()
    {
        Assert.EndsWith("FFFFFF", TraceTickDefinition.GetPredefined().First(d => d.Name == "Row Access").Background);
    }

    [Fact]
    public void Row_Reads_Made_Searching_A_Page_Come_Before_The_Other_Row_Reads()
    {
        var rules = TraceTickDefinition.GetPredefined().Where(d => d.Name == "Row Access").ToList();

        Assert.Equal([("Search Page", "PageRef::SearchPage"), ("Row Access", null)],
                     rules.Select(r => (r.GetLabel(), r.Conditions.FirstOrDefault()?.Value)));
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
    public void An_Operator_Lights_Its_Active_Part_From_Its_Open_Until_It_Is_Closed()
    {
        var open = TraceTickDefinition.GetPredefined().Single(d => d.Name == "Open");

        Assert.Equal((TraceTickSourceType.Open, "Active"), (open.Source, open.PlanPart));
    }

    [Fact]
    public void The_Hash_Table_Build_Is_A_Phase()
    {
        var build = TraceTickDefinition.GetPredefined().Single(d => d is { Name: "Hash Table", Label: "Build" });

        Assert.Equal((true, "CQScanHash::ConsumeBuild"), (build.IsPhase, build.Function));
    }

    [Fact]
    public void The_Sort_Build_Is_A_Phase_Around_Its_Pushed_Rows()
    {
        var rules = TraceTickDefinition.GetPredefined().Where(d => d.Name == "Sort").ToList();

        Assert.Equal([("Build", "CQScanSortNew::BuildSortTable", true),
                      ("Push Row", "CQScanSortNew::PushRow", false),
                      ("Sorted Row", "RowsetSorted::GetNextSortedRowInternal", false)],
                     rules.Select(r => (r.GetLabel(), r.Function, r.IsPhase)));
    }

    [Fact]
    public void A_Stream_Aggregate_Group_Check_Comes_Before_The_Other_Evaluations()
    {
        const string groupCheck = "CQScanStreamAggregateNew::GetRow";

        var rules = TraceTickDefinition.GetPredefined().Where(d => d.Name == "Evaluations");

        Assert.Equal([("Same Group", groupCheck), ("Group Changed", groupCheck), ("False", null), ("NULL", null), ("True", null)],
                     rules.Select(r => (r.GetLabel(),
                                        r.Conditions.FirstOrDefault(c => c.Target == TraceConditionTarget.CalledDirectlyBy)?.Value)));
    }

    [Fact]
    public void A_Rule_Without_A_Title_Is_Titled_With_Its_Label()
    {
        var rule = new TraceTickDefinition("Evaluations", "*", "CEsExec::GeneralEval4", "#F04848", 1, 1, []) { Label = "False" };

        Assert.Equal(("False", "Evaluate Expression"), (rule.GetTitle(), (rule with { Title = "Evaluate Expression" }).GetTitle()));
    }

    [Fact]
    public void A_Rule_Without_A_Label_Is_Labelled_With_Its_Function()
    {
        var rule = new TraceTickDefinition("Compares", "SqlTsEs", "CEsExec::FastCompare1", "#FFFFFF", 1, 1, []);

        Assert.Equal("CEsExec::FastCompare1", rule.GetLabel());
    }
}
