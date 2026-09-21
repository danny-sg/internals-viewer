using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

public class QueryCropperTests
{
    [Fact]
    public void GetCropTiming_Pads_A_Long_Query_By_Ten_Milliseconds()
    {
        var (start, end) = QueryCropper.GetCropTiming([Statement(timeUs: 50_000, durationUs: 200_000)]);

        Assert.Equal(50_000 - 100 - 10_000, start);
        Assert.Equal(250_000 + 100 + 10_000, end);
    }

    [Fact]
    public void GetCropTiming_Pads_A_Short_Query_In_Proportion_To_Its_Length()
    {
        var read = new ReadEventGroup { Events = [], Pages = [], TimeUs = 21_000 };

        var (start, end) = QueryCropper.GetCropTiming([Statement(timeUs: 20_000, durationUs: 19_800), read]);

        Assert.Equal(19_900 - 2_000, start);
        Assert.Equal(39_900 + 2_000, end);
    }

    [Fact]
    public void GetCropTiming_Keeps_A_Minimum_Padding_On_A_Very_Short_Query()
    {
        var (start, end) = QueryCropper.GetCropTiming([Statement(timeUs: 20_000, durationUs: 800)]);

        Assert.Equal(19_900 - 500, start);
        Assert.Equal(20_900 + 500, end);
    }

    private static ExecutionOperatorEvent Statement(long timeUs, long durationUs) => new()
    {
        OperatorDescription = "SELECT",
        PlanNodeIdentifier = new PlanNodeIdentifier { NodeId = -1, PlanHandleId = 1 },
        TimeUs = timeUs,
        DurationUs = durationUs
    };
}
