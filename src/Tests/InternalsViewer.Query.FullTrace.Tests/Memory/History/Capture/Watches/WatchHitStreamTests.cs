using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Capture.Watches;

[Trait("Category", "Unit")]
public class WatchHitStreamTests
{
    private const uint Thread = 7;

    [Fact]
    public void Reads_Back_Every_Hit_With_Its_Bytes()
    {
        var random = new Random(1119);

        var stream = new WatchHitStream(Thread);

        var written = new List<(WatchHit Hit, byte[] Bytes)>();

        var sequence = 0x92ul;

        var steps = 0ul;

        var address = 0x21C_0BB4_0000ul;

        for (var count = 0; count < 5000; count++)
        {
            switch (random.Next(0, 6))
            {
                case 0:
                {
                    sequence += (ulong)random.Next(1, 100000);

                    steps = (ulong)random.Next(0, 1000);

                    break;
                }
                case 1:
                {
                    steps -= Math.Min(steps, (ulong)random.Next(0, 50));

                    break;
                }
                default:
                {
                    steps += (ulong)random.Next(0, 40);

                    break;
                }
            }

            address = random.Next(0, 4) == 0 ? 0x7FF0_0000_0000ul + (ulong)random.Next() : address + (ulong)random.Next(0, 3);

            var bytes = new byte[random.Next(0, 20) == 0 ? random.Next(1000, 9000) : random.Next(1, 9)];

            random.NextBytes(bytes);

            var hit = new WatchHit(sequence, steps, address, 0, Thread, (uint)bytes.Length, 0, 1);

            stream.Add(hit, bytes);

            written.Add((hit, bytes));
        }

        var reader = stream.GetReader();

        foreach (var (hit, bytes) in written)
        {
            Assert.True(reader.TryRead(out var read, out var data));

            Assert.Equal((hit.Position, hit.Address, Thread, bytes.Length), (read.Position, read.Address, read.Thread, read.Size));

            Assert.Equal(bytes, data.AsSpan(read.DataOffset, read.Size).ToArray());
        }

        Assert.False(reader.TryRead(out _, out _));
    }

    [Fact]
    public void Knows_When_A_Thread_Went_Back_In_Position()
    {
        var stream = new WatchHitStream(Thread);

        stream.Add(new WatchHit(5, 0, 0x1000, 0, Thread, 1, 0, 1), [1]);

        Assert.True(stream.IsOrdered);

        stream.Add(new WatchHit(4, 9, 0x1001, 0, Thread, 1, 0, 1), [2]);

        Assert.False(stream.IsOrdered);
    }
}
