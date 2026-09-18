using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using InternalsViewer.UI.App.Services.Query.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline.Renderers;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TraceRendererTests
{
    private const float BandHeight = 20;

    private const float BandPadding = 2;

    private const float BandLabelWidth = 50;

    private static readonly TimelineBandVisibility ShowAll = new(ShowLocks: true, ShowLatches: true, ShowWaits: true);

    private static readonly PlanNodeIdentifier Node = new(1, 1);

    [Fact]
    public void Drops_A_Return_Rail_From_The_Operator_Bar_To_The_Bottom_Of_The_Read()
    {
        var operatorEvent = Operator();

        var read = new ReadEventGroup { Events = [], ReadType = ReadType.Cached, PlanNodeIdentifier = Node };

        using var render = Render([operatorEvent, read], [0, 10], Bar(operatorEvent, top: 4, bottom: 10));

        Assert.True(render.IsLit(60, 15));
        Assert.True(render.IsLit(60, 28));
        Assert.False(render.IsLit(60, 34));
        Assert.False(render.IsLit(70, 15));
    }

    [Fact]
    public void Starts_The_Rail_At_The_Object_Pool_Lookup_The_Read_Fed()
    {
        var operatorEvent = Operator();

        var lookup = new ObjectPoolEvent();

        var read = new ReadEventGroup { Events = [], ReadType = ReadType.Cached, PlanNodeIdentifier = Node, PoolLookup = lookup };

        using var render = Render([operatorEvent, lookup, read], [0, 10, 10], Bar(operatorEvent, top: 4, bottom: 10));

        Assert.False(render.IsLit(60, 15));
        Assert.True(render.IsLit(60, 38));
    }

    [Fact]
    public void Draws_A_Dotted_Call_Rail_At_The_Start_And_A_Return_Rail_At_The_End()
    {
        var operatorEvent = Operator();

        var read = new ReadEventGroup
        {
            Events = [],
            ReadType = ReadType.Cached,
            PlanNodeIdentifier = Node,
            DurationUs = 20_000,
        };

        using var render = Render([operatorEvent, read], [0, 10], Bar(operatorEvent, top: 4, bottom: 10));

        var callRail = Enumerable.Range(10, 12).Select(y => render.IsLit(60, y)).ToList();

        Assert.Contains(true, callRail);
        Assert.Contains(false, callRail);
        Assert.True(render.IsLit(80, 15));
        Assert.False(render.IsLit(70, 15));
    }

    [Fact]
    public void Draws_No_Rail_For_A_Read_Without_An_Operator_Or_Lookup()
    {
        var operatorEvent = Operator();

        var read = new ReadEventGroup { Events = [], ReadType = ReadType.Cached };

        using var render = Render([operatorEvent, read], [0, 10], Bar(operatorEvent, top: 4, bottom: 10));

        Assert.False(render.IsLit(60, 15));
    }

    [Fact]
    public void Drops_A_Log_Rail_From_The_Log_Band_To_The_Top_Of_The_Operator_Bar()
    {
        var operatorEvent = Operator();

        var log = new TransactionLogEvent { PlanNodeIdentifier = Node };

        using var render = Render([log, operatorEvent], [10, 0], Bar(operatorEvent, top: 24, bottom: 30));

        Assert.True(render.IsLit(60, 20));
        Assert.False(render.IsLit(62, 20));
        Assert.False(render.IsLit(60, 26));
    }

    private static ExecutionOperatorEvent Operator()
        => new() { OperatorDescription = string.Empty, PlanNodeIdentifier = Node };

    private static OperatorBar Bar(ExecutionOperatorEvent operatorEvent, float top, float bottom)
        => new(operatorEvent, 50, 150, top, bottom, (top + bottom) / 2, 1, 0, (top + bottom) / 2, bottom - top, SKColors.White);

    private static Rendered Render(EngineEvent[] events, double[] timesMs, OperatorBar bar)
    {
        var resources = new RenderResource();

        var bands = new TimelineBandSet();

        var definition = TimelineDefinitionBuilder.CreateDefault().Build(events, ShowAll);

        bands.Rebuild(definition, resources.LabelFont);

        var bandCount = bands.Active.Count;

        var frame = new TimelineFrame
        {
            Times = timesMs,
            Bands = bands,
            Definition = definition,
            BandTops = [.. Enumerable.Range(0, bandCount).Select(r => r * BandHeight)],
            BandHeights = [.. Enumerable.Repeat(BandHeight, bandCount)],
            CanvasWidth = 200,
            BandLabelWidth = BandLabelWidth,
            BandPadding = BandPadding,
            AxisUnitsPerMs = 1000,
            TimeToX = ms => (float)(BandLabelWidth + ms),
            BandMarkerWidth = _ => 1,
            ColourProvider = null,
            ShowThreads = false,
            BandColour = SKColors.Black,
            AlternateBandColour = SKColors.Black,
            MinTime = 0,
            XToTime = x => x - BandLabelWidth,
        };

        var bitmap = new SKBitmap(200, (int)(bandCount * BandHeight));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);

            new TraceRenderer(resources, new CurrentSelection()).Draw(canvas, frame, [bar]);
        }

        return new Rendered(bitmap, bands, resources);
    }

    private sealed class Rendered(SKBitmap bitmap, TimelineBandSet bands, RenderResource resources) : IDisposable
    {
        public bool IsLit(int x, int y) => bitmap.GetPixel(x, y) != SKColors.Black;

        public void Dispose()
        {
            bitmap.Dispose();

            bands.Dispose();

            resources.Dispose();
        }
    }
}
