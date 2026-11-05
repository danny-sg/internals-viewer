using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class RecentRowFilterTests
{
    private const uint Thread = 9;

    private static readonly PageAddress Page = new(1, 405);

    [Fact]
    public void A_Row_Read_Again_After_A_Look_At_The_Next_Row_Is_Recent()
    {
        var filter = new RecentRowFilter();

        TracePageAccess[] rows = [Row(0x5B3), Row(0x611), Row(0x5B3), Row(0x611), Row(0x700), Row(0x611)];

        Assert.Equal([false, false, true, true, false, true], rows.Select(r => filter.IsRecent(Thread, r)));
    }

    [Fact]
    public void A_Row_Three_Rows_Back_Is_Not_Recent()
    {
        var filter = new RecentRowFilter();

        Assert.Equal([false, false, false, false],
                     new[] { Row(0x5B3), Row(0x611), Row(0x700), Row(0x5B3) }.Select(r => filter.IsRecent(Thread, r)));
    }

    [Fact]
    public void A_Row_Read_On_Another_Thread_Is_Not_Recent()
    {
        var filter = new RecentRowFilter();

        Assert.Equal((false, false), (filter.IsRecent(Thread, Row(0x5B3)), filter.IsRecent(Thread + 1, Row(0x5B3))));
    }

    private static TracePageAccess Row(int offset) => new(At(1), Page, 6, offset, 0, 0);
}
