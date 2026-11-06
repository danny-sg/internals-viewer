using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline;

[Trait("Category", "Unit")]
public class TraceScaleTests
{
    [Fact]
    public void Every_Position_Outside_A_Cut_Comes_Back_From_Its_Value()
    {
        const ulong steps = 9001;

        var scale = new TraceScale(steps);

        scale.SetCuts([(new TracePosition(40_000, 17), new TracePosition(40_013, 4_321))]);

        var positions = Enumerable.Range(0, 40)
                                  .SelectMany(s => new ulong[] { 0, 1, steps / 2, steps - 2, steps - 1 }
                                                       .Select(p => new TracePosition(39_990 + (ulong)s, p)))
                                  .Where(p => p < new TracePosition(40_000, 17) || p >= new TracePosition(40_013, 4_321));

        Assert.All(positions, p => Assert.Equal(p, scale.GetPosition(scale.GetValue(p))));
    }
}
