using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Timeline;
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

    private const int OtherNode = 8;

    private static readonly SKColor WriterColour = new(200, 40, 40);

    private static readonly SKColor OtherColour = new(40, 160, 40);

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
    public void Bands_With_No_Writes_In_View_Give_Their_Rows_To_The_Active_Ones()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0, 0x40)),
                                   Block(1, 0x2000_0000, 0x40, new TraceMemoryWrite(Point(90), 0, 0x40, WriterNode))]);

        using var _ = host;

        var before = pane.Blocks[pane.GetHit(new Point(10, PaneTop + 95))!.Value.Block].Block;

        host.ViewEnd = 50;

        pane.Render(Width, host.Scale);

        var after = pane.Blocks[pane.GetHit(new Point(10, PaneTop + 95))!.Value.Block].Block;

        Assert.Equal((1, 0), (before, after));
    }

    [Fact]
    public void More_Blocks_Than_Rows_Still_All_Fit_In_The_Pane()
    {
        var (pane, host) = Create([.. Enumerable.Range(0, 300).Select(b => Block(b, 0x1000_0000 + (ulong)b * 0x100, 8, Write(0, 8)))]);

        using var _ = host;

        Assert.True(pane.Blocks[pane.GetHit(new Point(41, PaneTop + PaneHeight - 0.5))!.Value.Block].Block >= 290);
    }

    [Fact]
    public async Task A_View_Change_Is_Blank_Until_The_Background_Frame_Arrives()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        host.ViewEnd = 50;

        using (var bitmap = new SKBitmap(Width, (int)(PaneTop + PaneHeight)))
        using (var canvas = new SKCanvas(bitmap))
        {
            pane.Draw(canvas, Width, host.Scale);
        }

        var before = (pane.GetHit(new Point(41, PaneTop + 20)).HasValue, host.IsProgressVisible);

        (await host.WaitForPost().WaitAsync(TimeSpan.FromSeconds(10)))();

        Assert.Equal(((false, true), (true, false)), (before, (pane.GetHit(new Point(41, PaneTop + 20)).HasValue, host.IsProgressVisible)));
    }

    [Fact]
    public void A_Small_Block_Keeps_A_Row()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x10000, Write(0, 0x10000)), Block(1, 0x2000_0000, 8, Write(0, 8))]);

        using var _ = host;

        Assert.Equal(1, pane.Blocks[pane.GetHit(new Point(10, PaneTop + 99.5))!.Value.Block].Block);
    }

    [Fact]
    public void Hovering_A_Write_Finds_Only_Its_Block()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        Assert.Equal((0, -1), pane.GetHit(new Point(41, PaneTop + 20)) is { } hit ? (hit.Block, hit.Write) : (-1, -1));
    }

    [Fact]
    public void Clicking_A_Write_Shows_Its_Block_At_The_Write()
    {
        var (pane, host) = Create([Block(3, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        pane.Click(new Point(41, PaneTop + 20));

        Assert.Equal(((3, 0x1000_0020ul, new TracePosition(20, 0)), (double?)20), (host.Shown, host.MovedTo));
    }

    [Fact]
    public void Double_Clicking_A_Write_Selects_Its_Block_And_Position()
    {
        var (pane, host) = Create([Block(3, 0x1000_0000, 0x40, Write(0x20, 8))]);

        using var _ = host;

        pane.DoubleClick(new Point(41, PaneTop + 20));

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

    [Fact]
    public void Selecting_A_Call_Turns_Other_Writers_Grey_In_The_Bands_It_Writes()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0x20, 8), new TraceMemoryWrite(Point(60), 0x20, 8, OtherNode))]);

        using var _ = host;

        host.SelectedCallNodeIndex = WriterNode;

        var writerSelected = GetDrawnColours(pane, host);

        host.SelectedCallNodeIndex = OtherNode;

        var otherSelected = GetDrawnColours(pane, host);

        Assert.Equal((true, false, true),
                     (writerSelected.Contains(WriterColour),
                      writerSelected.Contains(OtherColour),
                      writerSelected.Contains(TimelineColours.ToGrey(OtherColour))));
        Assert.Equal((false, true, true),
                     (otherSelected.Contains(WriterColour),
                      otherSelected.Contains(OtherColour),
                      otherSelected.Contains(TimelineColours.ToGrey(WriterColour))));
    }

    [Fact]
    public void Selecting_A_Call_Hides_The_Bands_It_Does_Not_Write_And_Gives_Their_Rows_To_The_Rest()
    {
        var (pane, host) = Create([Block(0, 0x1000_0000, 0x40, Write(0, 0x20), new TraceMemoryWrite(Point(60), 0x30, 0x10, OtherNode))]);

        using var _ = host;

        var before = pane.GetHit(new Point(10, PaneTop + 95))!.Value.Offset;

        host.SelectedCallNodeIndex = WriterNode;

        pane.Render(Width, host.Scale);

        var after = pane.GetHit(new Point(10, PaneTop + 95))!.Value.Offset;

        Assert.Equal((true, true), (before >= 0x30, after < 0x20));
    }

    private static (TraceMemoryPane Pane, FakeHost Host) Create(TraceMemoryBlock[] blocks)
    {
        var host = new FakeHost();

        var pane = new TraceMemoryPane(host, () => (PaneTop, PaneHeight));

        pane.SetBlocks(blocks);

        pane.Render(Width, host.Scale);

        return (pane, host);
    }

    private static HashSet<SKColor> GetDrawnColours(TraceMemoryPane pane, FakeHost host)
    {
        using var bitmap = new SKBitmap(Width, (int)(PaneTop + PaneHeight));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);

            pane.Render(Width, host.Scale);

            pane.Draw(canvas, Width, host.Scale);
        }

        return [.. bitmap.Pixels];
    }

    private static TraceMemoryBlock Block(int id, ulong address, int size, params TraceMemoryWrite[] writes)
        => new(id, $"Object {id}", address, size, Point(1), null, new TraceMemoryWrites(writes));

    private static TraceMemoryWrite Write(int offset, int length) => new(Point(20), offset, length, WriterNode);

    private static TracePoint Point(ulong sequence) => new(new TracePosition(sequence, 0), sequence);

    private sealed class FakeHost : ITraceTimelineHost, IDisposable
    {
        public TraceTimelineAxis Axis => TraceTimelineAxis.Position;

        public double ViewStart { get; set; }

        public double ViewEnd { get; set; } = 100;

        public int Width => TraceMemoryPaneTests.Width;

        public double Scale => 2;

        public double? Playhead => null;

        public bool ShowCallStack => false;

        public int? SelectedCallNodeIndex { get; set; }

        public TraceTimelinePaints Paints { get; } = new();

        public SKPathBuilder PathBuilder { get; } = new();

        public (int Block, TracePosition? Position)? Selected { get; private set; }

        public (int Block, ulong Address, TracePosition Position)? Shown { get; private set; }

        private TaskCompletionSource<Action> Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public double? MovedTo { get; private set; }

        public bool IsProgressVisible { get; private set; }

        public double GetOrigin(TraceTimelineAxis axis) => 0;

        public SKColor GetColour(int callNodeIndex) => callNodeIndex switch
        {
            WriterNode => WriterColour,
            OtherNode => OtherColour,
            _ => SKColors.Gray
        };

        public string GetLabel(int callNodeIndex) => $"Writer {callNodeIndex}";

        public void Invalidate()
        {
        }

        public void Post(Action action) => Posted.TrySetResult(action);

        public Task<Action> WaitForPost() => Posted.Task;

        public void ShowMemoryProgress(bool isVisible) => IsProgressVisible = isVisible;

        public void MovePlayhead(double position) => MovedTo = position;

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

        public void ShowMemory(int block, ulong address, TracePosition position) => Shown = (block, address, position);

        public void Dispose()
        {
            Paints.Dispose();

            PathBuilder.Dispose();
        }
    }
}
