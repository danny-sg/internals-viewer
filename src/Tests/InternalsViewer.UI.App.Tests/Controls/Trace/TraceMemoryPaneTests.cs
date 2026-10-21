using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Trace;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Tests.Controls.Trace;

[Trait("Category", "Unit")]
public class TraceMemoryPaneTests
{
    private const float PaneTop = 10;

    private const float PaneHeight = 100;

    private const int Width = 200;

    private const int WriterNode = 7;

    private static readonly SKColor WriterColour = new(200, 40, 40);

    [Fact]
    public void Only_Bytes_That_Change_Take_Rows()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0, 8), Write(0x30, 8))]);

        using var _ = host;

        Assert.Equal([0, 0x30], new[] { 5f, 50f }.Select(y => pane.GetHit(new Point(10, PaneTop + y))!.Value.Offset));
    }

    [Fact]
    public void Blocks_Are_Separated_By_A_One_Pixel_Gap()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 8, Write(0, 8)), Block(1, 0x1000_0008, 8, Write(0, 8))]);

        using var _ = host;

        Assert.Equal([0, -1, 1], new[] { 47.5f, 48.5f, 49.5f }.Select(y => pane.GetHit(new Point(10, PaneTop + y)) is { } hit
                                                                             ? pane.Blocks[hit.Block].Block
                                                                             : -1));
    }

    [Fact]
    public void Tracked_Memory_Is_Packed_In_Address_Order()
    {
        var (pane, host) = Create([Block(0, 0x2000_0000, 0x40, Write(0, 0x40)), Block(1, 0x1000_0000, 0x40, Write(0, 0x40))]);

        using var _ = host;

        Assert.Equal([1, 0], new[] { 1f, 99f }.Select(y => pane.Blocks[pane.GetHit(new Point(10, PaneTop + y))!.Value.Block].Block));
    }

    [Fact]
    public void A_Small_Block_Keeps_A_Row()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x10000, Write(0, 0x10000)), Block(1, 0x2000_0000, 8, Write(0, 8))]);

        using var _ = host;

        Assert.Equal(1, pane.Blocks[pane.GetHit(new Point(10, PaneTop + 99.5))!.Value.Block].Block);
    }

    [Fact]
    public void A_Write_Under_The_Pointer_Is_Hit()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        Assert.Equal(new TraceMemoryHit(0, 0, 0x20), pane.GetHit(new Point(41, PaneTop + 20)));
    }

    [Fact]
    public void Clicking_A_Write_Selects_Its_Block_And_Position()
    {
        var (pane, host) = Create([Block(3, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        pane.Click(new Point(41, PaneTop + 20));

        Assert.Equal((3, (TracePosition?)new TracePosition(20, 0)), host.Selected);
    }

    [Fact]
    public void Draws_Lifetimes_In_Grey_And_Writes_In_The_Writer_Colour()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        using var bitmap = new SKBitmap(Width, (int)(PaneTop + PaneHeight));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);

            pane.Draw(canvas, Width, host.Scale);
        }

        var lifetime = bitmap.GetPixel(10, (int)PaneTop + 5);

        Assert.True(lifetime.Red == lifetime.Green && lifetime.Green == lifetime.Blue && lifetime.Red > 30);
        Assert.Equal(WriterColour, bitmap.GetPixel(40, (int)PaneTop + 24));
    }

    private static (TraceMemoryPane Pane, FakeHost Host) Create(TraceMemoryBlock[] blocks)
    {
        var host = new FakeHost();

        var pane = new TraceMemoryPane(host, () => (PaneTop, PaneHeight));

        pane.SetBlocks(blocks);

        return (pane, host);
    }

    private static TraceMemoryBlock Block(int id, ulong address, int size, params TraceMemoryWrite[] writes)
        => new(id, $"Object {id}", address, size, Point(1), null, writes);

    private static TraceMemoryWrite Write(int offset, int length) => new(Point(20), offset, length, WriterNode);

    private static TracePoint Point(ulong sequence) => new(new TracePosition(sequence, 0), sequence);

    private sealed class FakeHost : ITraceTimelineHost, IDisposable
    {
        public TraceTimelineAxis Axis => TraceTimelineAxis.Position;

        public double ViewStart => 0;

        public double ViewEnd => 100;

        public int Width => TraceMemoryPaneTests.Width;

        public double Scale => 2;

        public double? Playhead => null;

        public bool ShowCallStack => false;

        public TraceTimelinePaints Paints { get; } = new();

        public SKPathBuilder PathBuilder { get; } = new();

        public (int Block, TracePosition? Position)? Selected { get; private set; }

        public double GetOrigin(TraceTimelineAxis axis) => 0;

        public SKColor GetColour(int callNodeIndex) => callNodeIndex == WriterNode ? WriterColour : SKColors.Gray;

        public string GetLabel(int callNodeIndex) => $"Writer {callNodeIndex}";

        public void Invalidate()
        {
        }

        public void MovePlayhead(double position)
        {
        }

        public void ClickCall(CallStackNode node, int callIndex)
        {
        }

        public void OpenCallTree(CallStackNode node, int callIndex)
        {
        }

        public void OnSelectedOperatorChanged(ExecutionOperatorEvent? selected)
        {
        }

        public void SelectMemoryWrite(int block, TracePosition? position) => Selected = (block, position);

        public void Dispose()
        {
            Paints.Dispose();

            PathBuilder.Dispose();
        }
    }
}
