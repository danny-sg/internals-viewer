using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.Tests.Services.Query.Trace;

[Trait("Category", "Unit")]
[Trait("Area", "Trace")]
public class TraceTickTracksTests
{
    private static TraceTickTrack Track { get; } = new("Track", TraceTickLocation.Bottom, 0, TraceTickSeries.Empty);

    private static TraceTickDefinition Rule { get; } = new("Track", string.Empty, string.Empty, string.Empty, 1, 1, []);

    private static TraceTickDefinition PhaseRule { get; } = Rule with { IsPhase = true, ReturnDescription = "Built" };

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
        Assert.Equal("[Id]*(20)", TraceTickTracks.ShortenExpression("[Demo].[dbo].[Table].[Id] as [c].[Id]*(20)"));

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
    public void An_Open_Tick_Does_Not_Hold_Other_Steps_Inside_It()
    {
        var open = PhaseRule with { Source = TraceTickSourceType.Open };

        List<TraceTrackStep> steps = [Step(1, 3, open), Step(2, 2, Rule), Step(1, 3, open, isReturn: true)];

        Assert.Equal([-1, -1, -1], TraceTickTracks.GetStepNesting(steps).Select(n => n.Parent));
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

    private static TraceTrackStep Step(ulong from, ulong to, TraceTickDefinition rule, bool isReturn = false)
    {
        var span = new TraceSpan(new TracePoint(Position(from), from), new TracePoint(Position(to), to), 0, 0, TraceSpanFlags.Returned);

        return new TraceTrackStep(Track, new TraceTick(0, 0, new CallTreeNode(), span, Operator, null), rule, isReturn);
    }

    private static TracePosition Position(ulong sequence) => new(sequence, 0);
}
