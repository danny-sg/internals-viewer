using System.Data;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Services.Query.Trace;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Services.Query.Trace;

[Trait("Category", "Unit")]
[Trait("Area", "Trace")]
public class TraceTickTracksTests
{
    private static TraceTickTrack Track { get; } = new("Track", TraceTickLocation.Bottom, 0, TraceTickSeries.Empty);

    private static TraceTrackRule Rule { get; } = new("Track", string.Empty, string.Empty, string.Empty, 1, 1, []);

    private static TraceTrackRule PhaseRule { get; } = Rule with { IsPhase = true, ReturnDescription = "Built" };

    private static ExecutionOperatorEvent Operator { get; } = new() { OperatorDescription = string.Empty };

    [Fact]
    public void Next_Goes_Into_A_Phase_A_Step_At_A_Time()
    {
        var steps = CreateSteps();

        Assert.Equal((2, 3), (TraceTickTracks.GetStepIndex(steps, Position(2), forward: true),
                              TraceTickTracks.GetStepIndex(steps, Position(3), forward: true)));
    }

    [Fact]
    public void Previous_Goes_To_The_Step_Before_The_Playhead()
    {
        var steps = CreateSteps();

        Assert.Equal((3, -1), (TraceTickTracks.GetStepIndex(steps, Position(5), forward: false),
                               TraceTickTracks.GetStepIndex(steps, Position(1), forward: false)));
    }

    [Fact]
    public void Next_Phase_Stops_At_The_Start_And_End_Of_Each_Phase()
    {
        var steps = CreateSteps();

        Assert.Equal([1, 4, -1], new[] { 0, 1, 4 }.Select(i => TraceTickTracks.GetPhaseStepIndex(steps, i, forward: true)));
    }

    [Fact]
    public void Previous_Phase_Goes_Back_To_The_Phase_Boundary_Before()
    {
        var steps = CreateSteps();

        Assert.Equal([4, 1, -1], new[] { 5, 4, 1 }.Select(i => TraceTickTracks.GetPhaseStepIndex(steps, i, forward: false)));
    }

    [Fact]
    public void A_Plan_Default_Rule_Is_Always_A_Plan_Step() => Assert.True(TraceTickTracks.IsPlanStep(Rule, new HashSet<string>()));

    [Fact]
    public void A_Rule_That_Is_Not_A_Plan_Default_Is_Not_A_Plan_Step_Until_Selected()
    {
        var rule = Rule with { IsPlanDefault = false };

        Assert.Equal((false, true),
                     (TraceTickTracks.IsPlanStep(rule, new HashSet<string>()),
                      TraceTickTracks.IsPlanStep(rule, new HashSet<string> { rule.GetPlanStepKey() })));
    }

    [Fact]
    public void A_Page_Link_Shows_The_Page_Address()
    {
        Assert.Equal("[(1:200)](page:1:200)", TraceTickTracks.GetPageLink(new PageAddress(1, 200), null));
    }

    [Fact]
    public void A_Page_Link_With_A_Slot_Links_To_The_Row()
    {
        Assert.Equal("[(1:200)](page:1:200:5)", TraceTickTracks.GetPageLink(new PageAddress(1, 200), 5));
    }

    [Theory]
    [InlineData("page:1:200", 1, 200, null)]
    [InlineData("page:3:2672:9", 3, 2672, 9)]
    [InlineData("page:1:200:0", 1, 200, 0)]
    public void A_Page_Link_Reads_Back_As_The_Page_And_Slot(string link, short fileId, int pageId, int? slot)
    {
        var isPage = TraceTickTracks.TryGetPageLink(new Uri(link), out var page, out var linkSlot);

        Assert.Equal((true, new PageAddress(fileId, pageId), (ushort?)slot), (isPage, page, linkSlot));
    }

    [Theory]
    [InlineData("https://internalsviewer.com")]
    [InlineData("page:1")]
    [InlineData("page:x:200")]
    public void Other_Links_Are_Not_Page_Links(string link)
    {
        Assert.False(TraceTickTracks.TryGetPageLink(new Uri(link), out _, out _));
    }

    [Fact]
    public void Plain_Text_Keeps_Only_The_Text_Of_A_Link()
    {
        var markdown = $"Read **row** data on Page {TraceTickTracks.GetPageLink(new PageAddress(1, 200), 5)} `GetRow`";

        Assert.Equal("Read row data on Page (1:200) GetRow", TraceTickTracks.ToPlainText(markdown));
    }

    [Fact]
    public void Steps_Inside_A_Call_Nest_Under_It_And_End_With_Its_Return()
    {
        var nesting = TraceTickTracks.GetStepNesting(CreateSteps());

        Assert.Equal([new TraceStepNesting(-1, 0),
                      new TraceStepNesting(-1, 4),
                      new TraceStepNesting(1, 2),
                      new TraceStepNesting(1, 3),
                      new TraceStepNesting(1, 4),
                      new TraceStepNesting(-1, 5)],
                     nesting);
    }

    [Fact]
    public void A_Call_Inside_A_Call_Nests_One_Level_Further()
    {
        List<TraceTrackStep> steps =
        [
            Step(1, 9, PhaseRule),
            Step(2, 5, PhaseRule),
            Step(3, 3, Rule),
            Step(2, 5, PhaseRule, isReturn: true),
            Step(1, 9, PhaseRule, isReturn: true)
        ];

        Assert.Equal([-1, 0, 1, 1, 0], TraceTickTracks.GetStepNesting(steps).Select(n => n.Parent));
    }

    [Fact]
    public void The_Pages_Of_A_Seek_Nest_Under_It_Until_The_Seek_Ends()
    {
        var seek = Rule with { Source = TraceTickSourceType.Seek };

        List<TraceTrackStep> steps = [Step(1, 4, seek), Step(2, 2, seek), Step(3, 3, seek), Step(5, 5, Rule)];

        Assert.Equal([new TraceStepNesting(-1, 2),
                      new TraceStepNesting(0, 1),
                      new TraceStepNesting(0, 2),
                      new TraceStepNesting(-1, 3)],
                     TraceTickTracks.GetStepNesting(steps));
    }

    [Fact]
    public void A_Step_At_The_Same_Point_As_A_Page_Nests_Under_The_Seek_Not_The_Page()
    {
        var seek = Rule with { Source = TraceTickSourceType.Seek };

        List<TraceTrackStep> steps = [Step(1, 9, seek), Step(2, 2, seek), Step(2, 2, Rule)];

        Assert.Equal([-1, 0, 0], TraceTickTracks.GetStepNesting(steps).Select(n => n.Parent));
    }

    [Fact]
    public void Only_A_Seek_With_Pages_Under_It_Sticks()
    {
        var seek = Rule with { Source = TraceTickSourceType.Seek };

        List<TraceTrackStep> steps = [Step(1, 4, seek), Step(2, 2, seek), Step(5, 5, Rule)];

        var nesting = TraceTickTracks.GetStepNesting(steps);

        Assert.Equal([true, false, false], steps.Select((s, index) => TraceTickTracks.IsStickyStep(s, index, nesting)));
    }

    [Theory]
    [InlineData(SqlDbType.Int, 0xDEAD_0000_0000_0005UL, "Id = 5")]
    [InlineData(SqlDbType.Int, 0xFFFF_FFFFUL, "Id = -1")]
    [InlineData(SqlDbType.BigInt, 5UL, "Id = 5")]
    [InlineData(SqlDbType.SmallInt, 0xFFFFUL, "Id = -1")]
    [InlineData(SqlDbType.TinyInt, 0xFFUL, "Id = 255")]
    public void An_Integer_Key_Is_Read_At_The_Width_Of_Its_Column(SqlDbType type, ulong value, string expected)
        => Assert.Equal(expected, KeyText([("Id", type)], [Key(VariantStatus.Value, value)]));

    [Fact]
    public void A_Compound_Key_Is_Written_In_Key_Order()
    {
        var text = KeyText([("OrderId", SqlDbType.Int), ("LineId", SqlDbType.SmallInt)],
                           [Key(VariantStatus.Value, 1), Key(VariantStatus.Value, 2)]);

        Assert.Equal("(OrderId, LineId) = (1, 2)", text);
    }

    [Fact]
    public void A_Different_Number_Of_Values_And_Columns_Has_No_Value_Text()
        => Assert.Null(KeyText([("OrderId", SqlDbType.Int), ("LineId", SqlDbType.SmallInt)], [Key(VariantStatus.Value, 1)]));

    [Fact]
    public void One_Column_That_Is_Not_An_Integer_Leaves_The_Whole_Key_Without_Value_Text()
    {
        var text = KeyText([("OrderId", SqlDbType.Int), ("Code", SqlDbType.VarChar)],
                           [Key(VariantStatus.Value, 1), Key(VariantStatus.Value, 2)]);

        Assert.Null(text);
    }

    [Fact]
    public void A_Key_That_Is_Not_An_Integer_Has_No_Value_Text()
        => Assert.Null(KeyText([("Id", SqlDbType.VarChar)], [Key(VariantStatus.Value, 5)]));

    [Fact]
    public void A_Key_Of_Unknown_Type_Has_No_Value_Text()
        => Assert.Null(KeyText([("Id", null)], [Key(VariantStatus.Value, 5)]));

    [Fact]
    public void A_Null_Key_Has_No_Value_Text()
        => Assert.Null(KeyText([("Id", SqlDbType.Int)], [Key(VariantStatus.Null, 0)]));

    [Fact]
    public void An_Open_Tick_Does_Not_Hold_Other_Steps_Inside_It()
    {
        var open = PhaseRule with { Source = TraceTickSourceType.Open };

        List<TraceTrackStep> steps = [Step(1, 3, open), Step(2, 2, Rule), Step(1, 3, open, isReturn: true)];

        Assert.Equal([-1, -1, -1], TraceTickTracks.GetStepNesting(steps).Select(n => n.Parent));
    }

    [Fact]
    public void A_Predicate_Tag_Is_Taken_Out_Of_The_Description_And_Resolved()
    {
        var step = PredicateStep("Evaluate<Predicate>{ExecutionPlan.Predicate}</Predicate>");

        var predicate = TraceTickTracks.GetStepPredicate(step)?.ToString();

        Assert.Equal(("Evaluate", "[Id] = 1"), (TraceTickTracks.GetStepDescription(step), predicate));
    }

    [Fact]
    public void A_Description_Without_A_Tag_Has_No_Predicate()
    {
        Assert.Null(TraceTickTracks.GetStepPredicate(PredicateStep("Evaluate")));
    }

    [Fact]
    public void A_Property_The_Operator_Does_Not_Have_Shows_No_Predicate()
    {
        Assert.Null(TraceTickTracks.GetStepPredicate(PredicateStep("<Predicate>{ExecutionPlan.Other}</Predicate>")));
    }

    [Fact]
    public void Tooltip_Text_Drops_The_Tags_But_Keeps_The_Value()
    {
        var track = Track with { GetPlaceholder = (_, _) => "[Id] = 1" };

        var tick = PredicateStep("Evaluate").Tick;

        const string description = "Evaluate <Predicate>{ExecutionPlan.Predicate}</Predicate>";

        Assert.Equal("Evaluate [Id] = 1", TraceTickTracks.GetText(track, tick, description));
    }

    [Fact]
    public void Tooltip_Text_Leaves_Out_A_Property_The_Operator_Does_Not_Have()
    {
        var track = TraceTickTracks.Create(null!,
                                           [Rule],
                                           new TraceTickTrackSeries(TraceTickSeries.Empty, [0]),
                                           0,
                                           _ => false,
                                           _ => string.Empty,
                                           (_, _) => null,
                                           _ => null!);

        var tick = PredicateStep("Evaluate").Tick;

        Assert.Equal("Evaluate", TraceTickTracks.GetText(track, tick, "Evaluate <Predicate>{ExecutionPlan.Predicate}</Predicate>"));
    }

    [Fact]
    public void The_Result_Placeholder_Is_The_Rule_Label()
    {
        var track = TraceTickTracks.Create(null!,
                                           [Rule with { Label = "True" }],
                                           new TraceTickTrackSeries(TraceTickSeries.Empty, [0]),
                                           0,
                                           _ => false,
                                           _ => string.Empty,
                                           (_, _) => null,
                                           _ => null!);

        var tick = PredicateStep("Evaluate").Tick;

        Assert.Equal("Evaluate `True`", TraceTickTracks.GetText(track, tick, "Evaluate `{Result}`"));
    }

    [Fact]
    public void Code_In_A_Description_With_A_Result_Takes_The_Rule_Colour()
    {
        var step = Step(1, 1, Rule with { Colour = "#5CD66B", Description = "Evaluate `{Result}`" });

        Assert.Equal("#5CD66B", TraceTickTracks.GetStepCodeColour(step));
    }

    [Fact]
    public void Code_In_A_Description_Without_A_Result_Takes_The_Category_Colour()
    {
        var step = Step(1, 1, Rule with { Colour = "#5CD66B", Description = "Evaluate `{Function}`" });

        Assert.Equal(step.Tick.Node.CategoryColour, TraceTickTracks.GetStepCodeColour(step));
    }

    [Fact]
    public void A_Hash_Link_Shows_The_Bucket_And_Gives_Back_The_Group_And_Partition()
    {
        var link = TraceTickTracks.GetHashLink(0x16F6788B820, 3, 42);

        var start = link.IndexOf('(');

        Assert.True(TraceTickTracks.TryGetHashLink(new Uri(link[(start + 1)..^1]), out var group, out var partition));

        Assert.Equal(("[42]", 0x16F6788B820UL, 3), (link[..start], group, partition));
    }

    [Fact]
    public void A_Row_Id_Shows_As_File_Page_And_Slot()
    {
        var rowId = new Locator(1, LocatorValueType.Row, (115UL << 48) | (1UL << 32) | 2675);

        Assert.Equal("RID = (1:2675:115)", TraceTickTracks.GetRowIdText(rowId).ToString());
    }

    private static VariantLocator Key(VariantStatus status, ulong value) => new(VariantLocator.Create(1, status, value, 4));

    private static string? KeyText(List<(string Column, SqlDbType? Type)> columns, List<VariantLocator> keys)
        => TraceTickTracks.GetSeekKeyText(columns, keys)?.ToString();

    private static TraceTrackStep PredicateStep(string description)
    {
        var text = new PredicateText([new PredicateToken(PredicateTokenType.Column, "[Id] = 1")]);

        var track = Track with { GetPredicate = (_, name) => name == "ExecutionPlan.Predicate" ? text : null };

        return Step(1, 1, Rule with { Description = description }) with { Track = track };
    }

    private static List<TraceTrackStep> CreateSteps()
        =>
        [
            Step(1, 1, Rule),
            Step(2, 5, PhaseRule),
            Step(3, 3, Rule),
            Step(4, 4, Rule),
            Step(2, 5, PhaseRule, isReturn: true),
            Step(6, 6, Rule)
        ];

    private static TraceTrackStep Step(ulong from, ulong to, TraceTrackRule rule, bool isReturn = false)
    {
        var span = new TraceSpan(new TracePoint(Position(from), from), new TracePoint(Position(to), to), 0, 0, TraceSpanFlags.Returned);

        return new TraceTrackStep(Track, new TraceTick(0, 0, new CallTreeNode(), span, Operator, null), rule, isReturn);
    }

    private static TracePosition Position(ulong sequence) => new(sequence, 0);
}
