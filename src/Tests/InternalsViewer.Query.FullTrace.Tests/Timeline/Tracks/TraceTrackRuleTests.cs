using System.Text;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceTrackRuleTests
{
    [Fact]
    public void The_Predefined_Rules_Make_The_Built_In_Tracks_In_Order()
    {
        Assert.Equal(["GetRow", "Read", "Evaluations", "Page", "Seek", "Hash Table", "Hash Aggregate", "Sort", "Compute Scalar",
                      "Watches", "Statement", "Open"],
                     TraceTrackRule.GetPredefined().Select(d => d.Name).Distinct());
    }

    [Fact]
    public void A_Rule_Keeps_Its_Label_Source_And_Value_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Seek", string.Empty, string.Empty, "#4472C4", 1, 1, [])
        {
            Label = "Leaf Page",
            Source = TraceTickSourceType.Seek,
            Value = 3
        };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTrackRule.Read(stream));

        Assert.Equal(("Leaf Page", TraceTickSourceType.Seek, (byte)3), (read.Label, read.Source, read.Value));
    }

    [Fact]
    public void A_Rule_Keeps_Its_Plan_Part_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Seek", string.Empty, string.Empty, "#4472C4", 1, 1, []) { PlanPart = "Index Seek.Root Node" };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        Assert.Equal("Index Seek.Root Node", Assert.Single(TraceTrackRule.Read(stream)).PlanPart);
    }

    [Fact]
    public void A_Rule_Keeps_Its_Background_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Row Access", string.Empty, string.Empty, "Category", 1, 1, []) { Background = "#FFFFFF" };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        Assert.Equal("#FFFFFF", Assert.Single(TraceTrackRule.Read(stream)).Background);
    }

    [Fact]
    public void A_Rule_Keeps_Its_Description_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Reads", string.Empty, string.Empty, "#9B6BD6", 1, 1, [])
        {
            Description = "The PFS is used to check if an IAM extent is allocated"
        };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTrackRule.Read(stream));

        Assert.Equal("The PFS is used to check if an IAM extent is allocated", read.Description);
    }

    [Fact]
    public void A_Rule_Keeps_Its_Return_Description_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("GetRow", string.Empty, string.Empty, "#00B050", 1, 1, [])
        {
            ReturnDescription = "GetRow returns to parent operator"
        };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        var read = Assert.Single(TraceTrackRule.Read(stream));

        Assert.Equal("GetRow returns to parent operator", read.ReturnDescription);
    }

    [Fact]
    public void Every_GetRow_Rule_Describes_The_Call_And_The_Return()
    {
        var rules = TraceTrackRule.GetPredefined().Where(d => d.Name == "GetRow").ToList();

        Assert.All(rules, r => Assert.Equal(("{ParentOperatorName} → {OperatorName} `{RowCall}`", true),
                                            (r.Description, r.ReturnDescription?.Contains("{OperatorName}") == true)));
    }

    [Fact]
    public void Hash_Aggregate_Build_And_Merge_Are_Phases()
    {
        var rules = TraceTrackRule.GetPredefined().Where(d => d.Name == "Hash Aggregate").ToList();

        Assert.Equal(["Build", "Merge"], rules.Where(r => r.IsPhase).Select(r => r.GetLabel()));
    }

    [Fact]
    public void Every_Compute_Scalar_Rule_Is_An_Evaluation_That_Names_Its_Expression()
    {
        var rules = TraceTrackRule.GetPredefined().Where(d => d.Name == "Compute Scalar").ToList();

        Assert.NotEmpty(rules);

        Assert.All(rules, r => Assert.Equal((TraceTickSourceType.Evaluate, true),
                                            (r.Source, r.Description?.Contains("{Expression}") == true)));
    }

    [Fact]
    public void Initialising_The_Statement_Is_A_Phase_Without_A_Return()
    {
        var rule = TraceTrackRule.GetPredefined().Single(d => d.Name == "Statement");

        Assert.Equal((TraceTickSourceType.Statement, true, true),
                     (rule.Source, rule.IsPhase, string.IsNullOrEmpty(rule.ReturnDescription)));
    }

    [Fact]
    public void A_Placeholder_Can_Name_A_Plan_Property()
    {
        Assert.Equal("ExecutionPlan.Predicate", TraceTrackRule.Format("{ExecutionPlan.Predicate}", n => n));
    }

    [Fact]
    public void Every_Predefined_Rule_Has_A_Description()
    {
        Assert.All(TraceTrackRule.GetPredefined(), r => Assert.False(string.IsNullOrEmpty(r.Description), r.GetLabel()));
    }

    [Fact]
    public void Placeholders_Are_Replaced_By_Name_And_Unknown_Ones_Are_Left()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OperatorName"] = "Table Scan",
            ["ParentOperatorName"] = "Top"
        };

        var text = TraceTrackRule.Format("{ParentOperatorName} calls `GetRow` from {operatorname} at {Unknown}",
                                         n => values.GetValueOrDefault(n));

        Assert.Equal("Top calls `GetRow` from Table Scan at {Unknown}", text);
    }

    [Fact]
    public void The_Read_Track_Marks_IAM_And_PFS_Page_Fetches()
    {
        var fetches = TraceTrackRule.GetPredefined().Where(d => d is { Name: "Read", Source: TraceTickSourceType.PageFetch });

        Assert.Equal([(byte)PageType.Iam, (byte)PageType.Pfs], fetches.Select(d => d.Value));
    }

    [Fact]
    public void The_Page_Track_Has_A_White_Background()
    {
        Assert.EndsWith("FFFFFF", TraceTrackRule.GetPredefined().First(d => d.Name == "Page").Background);
    }

    [Fact]
    public void The_Page_Track_Holds_Row_And_Header_Accesses()
        => Assert.Equal(["Search Page", "Found", "Row Access", "Page Header Access"],
                        TraceTrackRule.GetPredefined().Where(d => d.Name == "Page").Select(d => d.GetLabel()));

    [Fact]
    public void The_Page_Header_Access_Track_Has_Its_Own_Source()
        => Assert.Equal(TraceTickSourceType.PageHeaderAccess,
                        TraceTrackRule.GetPredefined().Single(d => d.Label == "Page Header Access").Source);

    [Fact]
    public void A_Rule_Is_A_Plan_Default_Unless_The_File_Says_Otherwise()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """[{"Name":"T","Module":"","Function":"","Colour":"","HeightFactor":1,"WidthFactor":1,"Conditions":[]}]"""));

        Assert.True(Assert.Single(TraceTrackRule.Read(stream)).IsPlanDefault);
    }

    [Fact]
    public void A_Rule_That_Is_Not_A_Plan_Default_Stays_That_Way_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Page", string.Empty, string.Empty, "#FFFFFF", 1, 1, []) { IsPlanDefault = false };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        Assert.False(Assert.Single(TraceTrackRule.Read(stream)).IsPlanDefault);
    }

    [Fact]
    public void A_Detailed_Rule_Stays_Detailed_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Sort", string.Empty, string.Empty, "#FFFFFF", 1, 1, []) { IsDetailed = true };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        Assert.True(Assert.Single(TraceTrackRule.Read(stream)).IsDetailed);
    }

    [Fact]
    public void Only_Sort_And_Hash_Table_Rules_Are_Detailed()
        => Assert.Equal(["Hash Table", "Sort"],
                        TraceTrackRule.GetPredefined().Where(d => d.IsDetailed).Select(d => d.Name).Distinct().Order());

    [Fact]
    public void Only_Page_Header_Access_And_Open_Are_Not_Plan_Defaults()
        => Assert.Equal(["Page Header Access", "Open"],
                        TraceTrackRule.GetPredefined().Where(d => !d.IsPlanDefault).Select(d => d.GetLabel()));

    [Fact]
    public void A_Plan_Step_Key_Tells_Rules_On_Different_Tracks_Apart()
    {
        var hash = new TraceTrackRule("Hash Table", string.Empty, string.Empty, string.Empty, 1, 1, []) { Label = "Build" };

        Assert.NotEqual(hash.GetPlanStepKey(), (hash with { Name = "Sort" }).GetPlanStepKey());
    }

    [Fact]
    public void Header_Fields_In_Backticks_Are_Each_Quoted()
        => Assert.Equal("Read `Page Type`, `Internal Object Id` from Header",
                        TraceTrackRule.Format("Read `{HeaderField}` from Header", _ => "Page Type, Internal Object Id"));

    [Fact]
    public void One_Header_Field_In_Backticks_Is_Quoted_Once()
        => Assert.Equal("Read `Slot Count`", TraceTrackRule.Format("Read `{HeaderField}`", _ => "Slot Count"));

    [Fact]
    public void Header_Fields_Without_Backticks_Stay_As_One_List()
        => Assert.Equal("Read Page Type, Slot Count", TraceTrackRule.Format("Read {HeaderField}", _ => "Page Type, Slot Count"));

    [Fact]
    public void A_Function_In_Backticks_Keeps_The_Commas_In_Its_Name()
        => Assert.Equal("`Run<int, int>`", TraceTrackRule.Format("`{Function}`", _ => "Run<int, int>"));

    [Fact]
    public void A_Placeholder_Without_A_Value_Stays_As_Written()
        => Assert.Equal("`{Unknown}` and {Other}", TraceTrackRule.Format("`{Unknown}` and {Other}", _ => null));

    [Fact]
    public void The_Page_Header_Access_Description_Names_The_Field_Read()
        => Assert.Contains($"{{{TraceTrackRule.HeaderFieldPlaceholder}}}",
                           TraceTrackRule.GetPredefined().Single(d => d.Label == "Page Header Access").Description);

    [Fact]
    public void Row_Reads_Made_Searching_A_Page_Come_Before_The_Other_Row_Reads()
    {
        var rules = TraceTrackRule.GetPredefined().Where(d => d.Source == TraceTickSourceType.RowAccess).ToList();

        Assert.Equal([("Search Page", "PageRef::SearchPage"), ("Found", "PageRef::SearchPage"), ("Row Access", null)],
                     rules.Select(r => (r.GetLabel(), r.Conditions.FirstOrDefault()?.Value)));
    }

    [Fact]
    public void Only_A_Counter_Rule_Writes_Its_Counter()
    {
        var rule = new TraceTrackRule("GetRow", string.Empty, string.Empty, "#00B050", 1, 1, []);

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule with { IsCounter = true }, rule]);

        var text = Encoding.UTF8.GetString(stream.ToArray());

        stream.Position = 0;

        var read = TraceTrackRule.Read(stream);

        Assert.Equal((1, true, false), (text.Split("IsCounter").Length - 1, read[0].IsCounter, read[1].IsCounter));
    }

    [Fact]
    public void An_Operator_Lights_Its_Active_Part_From_Its_Open_Until_It_Is_Closed()
    {
        var open = TraceTrackRule.GetPredefined().Single(d => d.Name == "Open");

        Assert.Equal((TraceTickSourceType.Open, "Active"), (open.Source, open.PlanPart));
    }

    [Fact]
    public void A_Build_Row_Is_Marked_When_The_Build_Inserts_It()
    {
        var rule = TraceTrackRule.GetPredefined().Single(d => d is { Name: "Hash Table", Label: "Insert" });

        var condition = Assert.Single(rule.Conditions);

        Assert.Equal(("CHashWorkfilePartitionGroup::Insert", TraceConditionTarget.CalledDirectlyBy, "CQScanHash::ConsumeBuild"),
                     (rule.Function, condition.Target, condition.Value));
    }

    [Fact]
    public void The_Hash_Table_Build_Is_A_Phase()
    {
        var build = TraceTrackRule.GetPredefined().Single(d => d is { Name: "Hash Table", Label: "Build" });

        Assert.Equal((true, "CQScanHash::ConsumeBuild"), (build.IsPhase, build.Function));
    }

    [Fact]
    public void The_Sort_Build_And_Output_Are_Phases_Around_Their_Rows()
    {
        var rules = TraceTrackRule.GetPredefined().Where(d => d.Name == "Sort" && !d.IsDetailed).ToList();

        Assert.Equal([("Build", "CQScanSortNew::BuildSortTable", true),
                      ("Push Row", "CQScanSortNew::PushRow", false),
                      ("Merge Runs", "lmsort", false),
                      ("Output", "sort_initoutput", true),
                      ("Sorted Row", "RowsetSorted::GetNextSortedRowInternal", false)],
                     rules.Select(r => (r.GetLabel(), r.Function, r.IsPhase)));
    }

    [Fact]
    public void A_Rule_Keeps_Its_Details_Through_The_File_Format()
    {
        var rule = new TraceTrackRule("Track", "sqlmin", "Function", "#FFFFFF", 1, 1, []) { Details = "Why it happens" };

        using var stream = new MemoryStream();

        TraceTrackRule.Write(stream, [rule]);

        stream.Position = 0;

        Assert.Equal("Why it happens", Assert.Single(TraceTrackRule.Read(stream)).Details);
    }

    [Fact]
    public void The_Iam_And_Pfs_Reads_Explain_Themselves_In_Their_Details()
    {
        var fetches = TraceTrackRule.GetPredefined().Where(d => d is { Name: "Read", Source: TraceTickSourceType.PageFetch });

        Assert.All(fetches, f => Assert.Equal((false, false), (string.IsNullOrEmpty(f.Details), f.Description!.Contains(". "))));
    }

    [Fact]
    public void The_Top_Watch_Follows_The_Rows_Still_To_Return()
    {
        var watch = TraceTrackRule.GetPredefined().Single(d => d.Source == TraceTickSourceType.Watch);

        Assert.Equal(("CQScanTopNew", 0x48, 8, "BigInt"), (watch.Function, watch.Offset, watch.Length, watch.DataType));
    }

    [Fact]
    public void A_Stream_Aggregate_Group_Check_Comes_Before_The_Other_Evaluations()
    {
        const string groupCheck = "CQScanStreamAggregateNew::GetRow";

        var rules = TraceTrackRule.GetPredefined().Where(d => d.Name == "Evaluations");

        Assert.Equal([("No Change", groupCheck), ("Change", groupCheck), ("False", null), ("NULL", null), ("True", null)],
                     rules.Select(r => (r.GetLabel(),
                                        r.Conditions.FirstOrDefault(c => c.Target == TraceConditionTarget.CalledDirectlyBy)?.Value)));
    }

    [Fact]
    public void A_Rule_Without_A_Title_Is_Titled_With_Its_Label()
    {
        var rule = new TraceTrackRule("Evaluations", "*", "CEsExec::GeneralEval4", "#F04848", 1, 1, []) { Label = "False" };

        Assert.Equal(("False", "Evaluate Expression"), (rule.GetTitle(), (rule with { Title = "Evaluate Expression" }).GetTitle()));
    }

    [Fact]
    public void A_Rule_Without_A_Label_Is_Labelled_With_Its_Function()
    {
        var rule = new TraceTrackRule("Compares", "SqlTsEs", "CEsExec::FastCompare1", "#FFFFFF", 1, 1, []);

        Assert.Equal("CEsExec::FastCompare1", rule.GetLabel());
    }
}
