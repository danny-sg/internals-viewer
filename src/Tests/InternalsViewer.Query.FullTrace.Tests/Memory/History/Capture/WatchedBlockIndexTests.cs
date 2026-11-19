using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Capture;

[Trait("Category", "Unit")]
public class WatchedBlockIndexTests
{
    private const ulong Base = 0x21C_0BB4_0000;

    [Fact]
    public void Finds_The_Innermost_Block_Live_At_The_Position()
    {
        List<WatchedBlock> blocks = [Block(Base, 0x2000, At(1), At(100)),
                                     Block(Base + 0x100, 0x40, At(10), At(20)),
                                     Block(Base + 0x100, 0x40, At(30), TracePosition.Unknown)];

        var index = new WatchedBlockIndex(blocks);

        Assert.Equal(1, index.GetInnermost(Base + 0x110, At(15)));
        Assert.Equal(0, index.GetInnermost(Base + 0x110, At(25)));
        Assert.Equal(2, index.GetInnermost(Base + 0x110, At(40)));
        Assert.Null(index.GetInnermost(Base + 0x1000, At(200)));
    }

    [Fact]
    public void Gives_The_Start_Of_The_Next_Live_Block_On_The_Same_Page()
    {
        List<WatchedBlock> blocks = [Block(Base + 0x10, 0x10, At(1), At(50)),
                                     Block(Base + 0x80, 0x10, At(60), At(90)),
                                     Block(Base + 0x200, 0x10, At(1), TracePosition.Unknown),
                                     Block(Base + 0x1000, 0x10, At(1), TracePosition.Unknown)];

        var index = new WatchedBlockIndex(blocks);

        Assert.Equal(Base + 0x200, index.GetNextLiveStart(Base + 0x20, At(55)));
        Assert.Equal(Base + 0x80, index.GetNextLiveStart(Base + 0x20, At(70)));
        Assert.Equal(ulong.MaxValue, index.GetNextLiveStart(Base + 0x300, At(70)));
    }

    [Fact]
    public void Matches_A_Scan_Of_Every_Block()
    {
        var random = new Random(1119);

        var blocks = new List<WatchedBlock>();

        for (var count = 0; count < 600; count++)
        {
            var address = Base + (ulong)random.Next(0, 0x800) * 8;

            var size = random.Next(0, 10) == 0 ? (ulong)random.Next(0x1000, 0x3000) : (ulong)random.Next(1, 0x100);

            var from = (ulong)random.Next(0, 1000);

            var to = random.Next(0, 8) == 0 ? TracePosition.Unknown : At(from + (ulong)random.Next(1, 300));

            blocks.Add(Block(address, size, At(from), to));
        }

        var index = new WatchedBlockIndex(blocks);

        Compare(blocks, index, random);

        index.EndReusedBlocks();

        index.ClipToContainers();

        index.AddIterators(new Dictionary<ulong, TracePosition> { [Base + 0x48] = At(500), [Base + 0x1F00] = At(700) });

        Compare(blocks, index, random);
    }

    private static void Compare(List<WatchedBlock> blocks, WatchedBlockIndex index, Random random)
    {
        for (var query = 0; query < 20000; query++)
        {
            var address = Base + (ulong)random.Next(0, 0x5000);

            var position = At((ulong)random.Next(0, 1400), (ulong)random.Next(0, 2));

            var innermost = GetInnermost(blocks, address, position);

            Assert.Equal(innermost, index.GetInnermost(address, position));

            Assert.Equal(GetNextLiveStart(blocks, address, position, ulong.MaxValue), index.GetNextLiveStart(address, position));

            if (innermost is { } found)
            {
                Assert.Equal(GetNextLiveStart(blocks, address, position, blocks[found].Size), index.GetNextInner(found, address, position));
            }

            var size = (ulong)random.Next(0, 0x200);

            Assert.Equal(IsLive(blocks, address, size, position), index.IsLive(address, size, position));
        }
    }

    private static int? GetInnermost(List<WatchedBlock> blocks, ulong address, TracePosition position)
    {
        int? found = null;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index].Contains(address, position) && (found is not { } current || blocks[index].Size < blocks[current].Size))
            {
                found = index;
            }
        }

        return found;
    }

    private static ulong GetNextLiveStart(List<WatchedBlock> blocks, ulong address, TracePosition position, ulong size)
    {
        var next = ulong.MaxValue;

        foreach (var block in blocks)
        {
            if (block.Address > address
                && block.Address >> WatchedBlockIndex.PageShift == address >> WatchedBlockIndex.PageShift
                && block.Size < size
                && block.FromPosition <= position
                && position < block.ToPosition)
            {
                next = Math.Min(next, block.Address);
            }
        }

        return next;
    }

    private static bool IsLive(List<WatchedBlock> blocks, ulong address, ulong size, TracePosition position)
        => size > 0 && blocks.Exists(b => b.Address < address + size
                                          && address < b.Address + b.Size
                                          && b.FromPosition <= position
                                          && position < b.ToPosition);

    private static WatchedBlock Block(ulong address, ulong size, TracePosition from, TracePosition to)
        => new(0, address, size, isIterator: false, isInline: false, isClerkPage: false, 1, from, to);
}
